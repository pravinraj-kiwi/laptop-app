using Polly;
using Polly.Retry;
using Prism.Events;
using PursuitAlert.Domain.Application.Events;
using PursuitAlert.Domain.Device.Events;
using PursuitAlert.Domain.Device.Payloads.Events;
using PursuitAlert.Domain.Device.Payloads.Models;
using PursuitAlert.Domain.Modes.Events;
using PursuitAlert.Domain.Modes.Models;
using PursuitAlert.Domain.Modes.Services;
using PursuitAlert.Domain.Publishing.Events;
using PursuitAlert.Domain.Publishing.Models;
using PursuitAlert.Domain.Publishing.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Application.Publishing.Services
{
    public class BackgroundJobService : IBackgroundJobService
    {
        #region Properties

        public List<BackgroundJob> RunningJobs { get; set; } = new List<BackgroundJob>();

        private Mode AllClearModeConfiguration => _modePolicy.Execute(() => _modeService.ModeConfiguration.First(m => m.Name.Equals(AllClearMode)));

        private Mode PatrolModeConfiguration => _modePolicy.Execute(() => _modeService.ModeConfiguration.First(m => m.Name.Equals(PatrolMode)));

        #endregion Properties

        #region Fields

        private const string AllClearMode = "AllClearMode";

        private const string PatrolMode = "PatrolMode";

        private const string PowerOff = "PowerOff";

        private const string PowerOn = "PowerOn";

        private readonly IEventAggregator _eventAggregator;

        private readonly IMessageBuilderService _messageBuilder;

        private readonly RetryPolicy _modePolicy;

        private readonly IModeService _modeService;

        private readonly IPublishingService _publishingService;

        private object _lockObject = new object();

        private DeviceCoordinatesPayload PreviousCoordinates;

        #endregion Fields

        #region Constructors

        public BackgroundJobService(IPublishingService publishingService, IModeService modeService, IMessageBuilderService messageBuilder, IEventAggregator eventAggregator)
        {
            _publishingService = publishingService;
            _messageBuilder = messageBuilder;
            _modeService = modeService;
            _eventAggregator = eventAggregator;

            SetEventSubscriptions();

            _modePolicy = Policy
                .Handle<ArgumentNullException>()
                .WaitAndRetry(3, attemptNumber => TimeSpan.FromSeconds(attemptNumber), (ex, attemptNumber) =>
                {
                    Log.Warning(ex, "[BackgroundJobService] Modes have not yet been initailized. Retrying ({attempt}).", attemptNumber);
                });
        }

        #endregion Constructors

        #region Methods

        private void ExecuteJob(BackgroundJob job, DeviceCoordinatesPayload coordinatesPayload)
        {
            if (!ShouldExecuteJob(job))
                return;

            try
            {
                var message = _messageBuilder.BuildMessage(job.Mode, coordinatesPayload, job.IsClearMessage);
                Log.Debug("Sending {0} message", job.Mode.Message);

                _publishingService.SendTelemetry(message);
                job.LastMessageSent = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send message");
            }
        }

        private void HandleCoordinatesPayloadReceived(DeviceCoordinatesPayload coordinatesPayload)
        {
            lock (_lockObject)
            {
                foreach (var job in RunningJobs)
                    ExecuteJob(job, coordinatesPayload);

                // Remove all onetime jobs or 'event end jobs' after execution
                var oneTimeJobs = RunningJobs
                    .Where(j => j.Mode.PayloadKind == PayloadKind.OneTime || j.IsClearMessage)
                    .Select(j => j.Mode)
                    .ToList();

                foreach (var job in oneTimeJobs)
                    StopJob(job, true);

                PreviousCoordinates = coordinatesPayload;
            }
        }

        private void HandleDeviceDisconnect()
        {
            Log.Debug("Sending Power Off message after device disconnect");
            var powerOffMode = _modeService.ModeConfiguration.First(m => m.Name.Equals(PowerOff));
            ExecuteJob(new BackgroundJob(powerOffMode), PreviousCoordinates);
            Log.Verbose("Power Off message queued");

            // After the device is disconnected, we don't need to listen for any more disconnected
            // events, but start listening to connected events
            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(HandleDeviceDisconnect);

            if (!_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceReconnect))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceReconnect);

            // Remove all running jobs so that when the device reconnects, it gets a fresh start
            RunningJobs.Clear();
        }

        private void HandleDeviceReconnect(SerialPort port)
        {
            // Reactivate Patrol mode after reconnecting
            HandlePatrolModeActivated();

            if (!_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleDeviceDisconnect))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleDeviceDisconnect);

            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceReconnect))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceReconnect);
        }

        private void HandleInitialConnect()
        {
            // After a device is connected, start listening for disconnects
            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleDeviceDisconnect);
        }

        private void HandleModeChange(ModeChangeEventArgs modeChange)
        {
            // If a new mode was engaged and the new mode has a payload
            if (modeChange.ChangeType == ModeChangeType.ModeEngaged && modeChange.NewMode.PayloadKind != PayloadKind.None)
            {
                Log.Debug("Starting {0} job for {1}", modeChange.NewMode.PayloadKind, modeChange.NewMode.Name);

                // Stop the Patrol Mode job if it's running
                HandlePatrolModeDeactivated();

                RunningJobs.Add(new BackgroundJob(modeChange.NewMode));
            }
            else if (modeChange.ChangeType == ModeChangeType.ModeDisengaged)
            {
                Log.Debug("Stopping {0} job for {1}", modeChange.OriginalMode.PayloadKind, modeChange.OriginalMode.Name);
                if (RunningJobs.Any(j => j.Mode.Name.Equals(modeChange.OriginalMode.Name)))
                {
                    // Only send the all clear after this if there aren't any other active modes
                    StopJob(modeChange.OriginalMode);

                    // Create a job to send up an event end message for this mode
                    var endJob = new BackgroundJob(modeChange.OriginalMode, true);
                    RunningJobs.Add(endJob);
                }

                if (modeChange.ActivatedModes.Count == 0)
                    HandlePatrolModeActivated();
            }
        }

        private void HandlePatrolModeActivated()
        {
            RunningJobs.Add(new BackgroundJob(PatrolModeConfiguration));
        }

        private void HandlePatrolModeDeactivated()
        {
            if (RunningJobs.Any(j => j.Mode.Name.Equals(PatrolMode)))
                StopJob(PatrolModeConfiguration);
        }

        private void HandlePinDrop(Mode mode)
        {
            Log.Debug("Executing Pin Drop job");
            RunningJobs.Add(new BackgroundJob(mode));
        }

        private void HandlePowerOff(int exitCode)
        {
            Log.Debug("Sending Power Off message");
            var powerOffMode = _modeService.ModeConfiguration.First(m => m.Name.Equals(PowerOff));
            RunningJobs.Add(new BackgroundJob(powerOffMode));

            // Wait for 2 seconds so that the background job fires
            Task.Delay(2000).Wait();

            Log.Verbose("Power Off message queued");

            Log.Verbose("Releasing event subscriptions");
            _eventAggregator.GetEvent<ModeChangeEvent>().Unsubscribe(HandleModeChange);
            _eventAggregator.GetEvent<PatrolStartedEvent>().Unsubscribe(HandlePatrolModeActivated);
            _eventAggregator.GetEvent<PatrolEndedEvent>().Unsubscribe(HandlePatrolModeDeactivated);
            _eventAggregator.GetEvent<CoordinatesPayloadReceivedEvent>().Unsubscribe(HandleCoordinatesPayloadReceived);
            _eventAggregator.GetEvent<PinDroppedEvent>().Unsubscribe(HandlePinDrop);
            _eventAggregator.GetEvent<BrokerInitialConnectEvent>().Unsubscribe(HandleInitialConnect);
            _eventAggregator.GetEvent<ApplicationExitEvent>().Unsubscribe(HandlePowerOff);
            Log.Verbose("Event subscriptions cleared");
        }

        private void SetEventSubscriptions()
        {
            _eventAggregator.GetEvent<ModeChangeEvent>().Subscribe(HandleModeChange);
            _eventAggregator.GetEvent<PatrolStartedEvent>().Subscribe(HandlePatrolModeActivated);
            _eventAggregator.GetEvent<PatrolEndedEvent>().Subscribe(HandlePatrolModeDeactivated);
            _eventAggregator.GetEvent<CoordinatesPayloadReceivedEvent>().Subscribe(HandleCoordinatesPayloadReceived);
            _eventAggregator.GetEvent<PinDroppedEvent>().Subscribe(HandlePinDrop);
            _eventAggregator.GetEvent<BrokerInitialConnectEvent>().Subscribe(HandleInitialConnect);
            _eventAggregator.GetEvent<ApplicationExitEvent>().Subscribe(HandlePowerOff, ThreadOption.PublisherThread);
        }

        private bool ShouldExecuteJob(BackgroundJob job)
        {
            // If there are any modes active that aren't PATROL mode, don't send PATROL mode messages
            if (job.Mode.Name == PatrolMode && RunningJobs.Any(j => j.Mode.PayloadKind == PayloadKind.Repeating && j.Mode.Name != PatrolMode))
            {
                Log.Warning("Device is in an active mode, but PATROL mode is also active. Refusing to send PATROL message");
                return false;
            }

            // If the job has been executed once (has a value for the LastMessageSent property) and
            // has a payload kind of repeating, check the mode's interval and only if that interval
            // of time has passed, send the payload
            // TODO: Update this logic to implement the distance-based intervals as requested by Tim
            if (job.LastMessageSent.HasValue && job.Mode.PayloadKind == PayloadKind.Repeating)
            {
                var timeSinceLastSend = DateTime.UtcNow - job.LastMessageSent.Value;

                // If not enough time has passed since the last send, don't execute the job
                if (timeSinceLastSend.TotalMilliseconds <= job.Mode.PayloadInterval)
                    return false;
                else
                    return true;
            }
            else
                return true;
        }

        private void StopJob(Mode mode, bool canSendAllClear = false)
        {
            RunningJobs.Remove(RunningJobs.First(j => j.Mode.Name.Equals(mode.Name)));

            // Send AllClear if configured and only if no other modes are currently active
            // Don't send ALLCLEARs since we're now using event-specific end messages
            //if (mode.SendAllClearWhenEnded && canSendAllClear)
            //    RunningJobs.Add(new BackgroundJob(AllClearModeConfiguration));
        }

        #endregion Methods
    }
}