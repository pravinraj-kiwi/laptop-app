using Amazon.Runtime;
using Newtonsoft.Json;
using Prism.Events;
using PursuitAlert.Client.Events;
using PursuitAlert.Client.Infrastructure.IoTData;
using PursuitAlert.Client.Properties;
using PursuitAlert.Client.Services.Device;
using PursuitAlert.Client.Services.Device.Events;
using PursuitAlert.Client.Services.Device.Payloads;
using PursuitAlert.Client.Services.GPS;
using PursuitAlert.Client.Services.GPS.Events;
using PursuitAlert.Client.Services.Modes;
using PursuitAlert.Client.Services.Modes.Events;
using PursuitAlert.Client.Services.Modes.Events.EventPayloads;
using PursuitAlert.Client.Services.Telemetry.Events;
using Serilog;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace PursuitAlert.Client.Services.Telemetry
{
    public class TelemetryService : ITelemetryService
    {
        #region Properties

        public bool IsListening => IsSendingTelemtry();

        public int SpeedDatapointsCollected { get; private set; }

        public Timer TelemetrySendTimer { get; private set; }

        public VehicleStatus VehicleStatus { get; private set; }

        #endregion Properties

        #region Fields

        /// <summary>
        /// The threshold for how far the vehicle must move before it's considered "in motion".
        /// (0.012427 miles is 20 meters/22 yards/66 feet)
        /// </summary>
        private const double DISTANCETHRESHOLDFORMOVINGINMILES = 0.012427;

        /// <summary>
        /// When on patrol, send telemetry at a maximum every 4 seconds
        /// </summary>
        private const int IDLETELEMETRYINTERVALINSECONDS = 10;

        /// <summary>
        /// The threshold for how much bearing must change before it's considered "significant"
        /// </summary>
        private const double SIGNIFICANTTHRESHOLDFORBEARINGCHANGE = 15;

        private readonly IDeviceService _deviceService;

        private readonly IEventAggregator _eventAggregator;

        private readonly ICalculationService _gpsService;

        private readonly IIoTDataService _iotData;

        private readonly IModeService _modeService;

        private Coordinates _lastSentCoordinates;

        private Coordinates _mostRecentCoordinates;

        private DateTime LastTelemetrySendTime;

        #endregion Fields

        #region Constructors

        public TelemetryService(IIoTDataService ioTDataService,
            IModeService modeService,
            ICalculationService gpsService,
            IDeviceService deviceService,
            IEventAggregator eventAggregator)
        {
            _iotData = ioTDataService;
            _modeService = modeService;
            _gpsService = gpsService;
            _deviceService = deviceService;
            _eventAggregator = eventAggregator;

            VehicleStatus = VehicleStatus.Stationary;

            if (!_eventAggregator.GetEvent<SystemSleepEvent>().Contains(HandleSystemSleep))
                _eventAggregator.GetEvent<SystemSleepEvent>().Subscribe(HandleSystemSleep);
        }

        #endregion Constructors

        #region Methods

        public async Task Initialize()
        {
            await _iotData.Authenticate();
        }

        public async Task SendPowerOff()
        {
            Log.Debug("Sending {mode} signal", _modeService.PowerOffMode.Message);
            SendTelemetry(_modeService.PowerOffMode, _mostRecentCoordinates);
        }

        public void StartSendingTelemetry()
        {
            if (!_eventAggregator.GetEvent<CoordinatesReceivedEvent>().Contains(HandleNewCoordinates))
                _eventAggregator.GetEvent<CoordinatesReceivedEvent>().Subscribe(HandleNewCoordinates);

            // When the first piece of telemetry comes in, send a Patrol signal
            if (!_eventAggregator.GetEvent<CoordinatesReceivedEvent>().Contains(SendInitialPatrolSignal))
                _eventAggregator.GetEvent<CoordinatesReceivedEvent>().Subscribe(SendInitialPatrolSignal);

            if (!_eventAggregator.GetEvent<ModeChangeEvent>().Contains(HandleModeChange))
                _eventAggregator.GetEvent<ModeChangeEvent>().Subscribe(HandleModeChange);

            if (!_eventAggregator.GetEvent<PinDroppedEvent>().Contains(HandlePinDrop))
                _eventAggregator.GetEvent<PinDroppedEvent>().Subscribe(HandlePinDrop);

            TelemetrySendTimer = new Timer(1000);
            TelemetrySendTimer.Elapsed += SendTelemetryIfNoneSentWithinThreshold;
            TelemetrySendTimer.Enabled = true;
            TelemetrySendTimer.Start();
        }

        public void StopSendingTelemetry(string reason = "")
        {
            if (_eventAggregator.GetEvent<CoordinatesReceivedEvent>().Contains(HandleNewCoordinates))
                _eventAggregator.GetEvent<CoordinatesReceivedEvent>().Unsubscribe(HandleNewCoordinates);

            // When the first piece of telemetry comes in, send a Patrol signal
            if (_eventAggregator.GetEvent<CoordinatesReceivedEvent>().Contains(SendInitialPatrolSignal))
                _eventAggregator.GetEvent<CoordinatesReceivedEvent>().Unsubscribe(SendInitialPatrolSignal);

            if (_eventAggregator.GetEvent<ModeChangeEvent>().Contains(HandleModeChange))
                _eventAggregator.GetEvent<ModeChangeEvent>().Unsubscribe(HandleModeChange);

            if (_eventAggregator.GetEvent<PinDroppedEvent>().Contains(HandlePinDrop))
                _eventAggregator.GetEvent<PinDroppedEvent>().Unsubscribe(HandlePinDrop);

            _eventAggregator.GetEvent<PatrolEndedEvent>().Publish();

            if (TelemetrySendTimer != null)
            {
                TelemetrySendTimer.Elapsed -= SendTelemetryIfNoneSentWithinThreshold;
                TelemetrySendTimer.Enabled = false;
                TelemetrySendTimer.Stop();
                TelemetrySendTimer.Dispose();
                TelemetrySendTimer = null;
            }
        }

        private bool CanSendData() => !string.IsNullOrEmpty(Settings.Default.DeviceSerialNumber) && !string.IsNullOrEmpty(Settings.Default.Stage);

        private string GetVehicleDisplayStatus(Coordinates coordinates)
        {
            var speed = 0.00;
            try
            {
                speed = Math.Round(coordinates.Speed, 2);
            }
            catch { }

            var bearingDirection = "N";
            try
            {
                bearingDirection = _gpsService.BearingToCardinal(coordinates.Bearing);
            }
            catch { }

            return $"vehicle is {VehicleStatus}, {bearingDirection} at {speed} mph";
        }

        private void HandleModeChange(ModeChangeEventPayload modeChange)
        {
            if (modeChange.ChangeType == ModeChangeType.Engaged)
            {
                // Send the first signal for this mode
                Log.Verbose("Sending initial telemetry signal after {mode} engaged", modeChange.NewMode.Message);
                SendTelemetry(modeChange.NewMode, _mostRecentCoordinates);
            }
            else
            {
                // Send the END signal for this event
                Log.Verbose("Sending final telemetry signal after {mode} disengaged", modeChange.NewMode.Message);
                SendTelemetry(modeChange.NewMode, _mostRecentCoordinates, true);

                if (_modeService.ActiveModes.Count == 0)
                {
                    // Send a patrol signal
                    Log.Verbose("Sending intial patrol signal after {mode} disengaged to signify back on patrol", modeChange.NewMode.Message);
                    SendPatrolSignal();
                }
            }
        }

        private void HandleNewCoordinates(CoordinatesPayload coordinates)
        {
            var displayStatus = GetVehicleDisplayStatus(coordinates.ToCoordinates());

            // If these are teh first coordinates, set them to the most recent coordinates
            if (_mostRecentCoordinates == null)
                _mostRecentCoordinates = coordinates.ToCoordinates();

            // If more than the threshold for bearing change has occurred and the vehicle is moving,
            // send up the new coordinates. This will help with the missing points on the map in the Portal
            if (VehicleStatus == VehicleStatus.Moving && SignificantBearingChanged(coordinates.ToCoordinates(), _mostRecentCoordinates, out double bearingChange))
            {
                Log.Verbose("Sending telemetry (significant bearing change ({bearingChange} change in degrees, {displayStatus})", bearingChange, displayStatus);
                SendTelemetry(coordinates.ToCoordinates());
            }
            else if (SignificantDistanceTraveled(coordinates.ToCoordinates(), _lastSentCoordinates, out double distance))
            {
                // Send out an event that the vehicle is moving
                if (VehicleStatus != VehicleStatus.Moving)
                {
                    VehicleStatus = VehicleStatus.Moving;
                    Log.Information("Detected vehicle is in motion ({distance} mi traveled since last coordinates)", Math.Round(distance, 6).ToString("0." + new string('#', 339)));
                    SendTelemetry(coordinates.ToCoordinates());
                    _eventAggregator.GetEvent<VehicleMovingEvent>().Publish();
                }
                else if (ShouldSendTelemetry(coordinates.ToCoordinates()))
                {
                    Log.Verbose("Sending telemetry (significant distance ({distance} mi, {displayStatus})", Math.Round(distance, 6).ToString("0." + new string('#', 339)), displayStatus);
                    SendTelemetry(coordinates.ToCoordinates());
                }
            }
            else
            {
                Log.Verbose("Significant distance has not been traveled ({distanceTraveled} mi) ({displayStatus})", Math.Round(distance, 6).ToString("0." + new string('#', 339)), displayStatus);

                // Send out an event that the vehicle has become stationary
                if (VehicleStatus != VehicleStatus.Stationary)
                {
                    Log.Information("Detected vehicle is stationary ({distance} mi traveled since last coordinates)", Math.Round(distance, 6).ToString("0." + new string('#', 339)));
                    VehicleStatus = VehicleStatus.Stationary;
                    SendTelemetry(coordinates.ToCoordinates());
                    _eventAggregator.GetEvent<VehicleStationaryEvent>().Publish();
                }

                if (_lastSentCoordinates == null)
                    _lastSentCoordinates = new Coordinates();
            }

            _mostRecentCoordinates = coordinates.ToCoordinates();
        }

        private void HandlePinDrop(Mode pinDrop)
        {
            SendTelemetry(pinDrop, _mostRecentCoordinates);
            Log.Debug("Pin drop telemetry sent: {coordinates}", JsonConvert.SerializeObject(_mostRecentCoordinates));
        }

        private void HandleSystemSleep()
        {
            TelemetrySendTimer.Stop();

            if (!_eventAggregator.GetEvent<SystemWakeEvent>().Contains(HandleSystemWake))
                _eventAggregator.GetEvent<SystemWakeEvent>().Subscribe(HandleSystemWake);

            if (_eventAggregator.GetEvent<SystemSleepEvent>().Contains(HandleSystemSleep))
                _eventAggregator.GetEvent<SystemSleepEvent>().Unsubscribe(HandleSystemSleep);
        }

        private void HandleSystemWake()
        {
            TelemetrySendTimer.Start();

            if (!_eventAggregator.GetEvent<SystemSleepEvent>().Contains(HandleSystemSleep))
                _eventAggregator.GetEvent<SystemSleepEvent>().Subscribe(HandleSystemSleep);

            if (_eventAggregator.GetEvent<SystemWakeEvent>().Contains(HandleSystemWake))
                _eventAggregator.GetEvent<SystemWakeEvent>().Unsubscribe(HandleSystemWake);
        }

        private bool IsSendingTelemtry()
        {
            if (_eventAggregator == null)
                return false;

            return _eventAggregator.GetEvent<CoordinatesReceivedEvent>().Contains(HandleNewCoordinates)
                && _eventAggregator.GetEvent<ModeChangeEvent>().Contains(HandleModeChange)
                && _eventAggregator.GetEvent<PinDroppedEvent>().Contains(HandlePinDrop)
                && TelemetrySendTimer != null;
        }

        private void SendInitialPatrolSignal(CoordinatesPayload coordinates)
        {
            var location = coordinates.ToCoordinates();
            SendTelemetry(_modeService.PatrolMode, location);

            _eventAggregator.GetEvent<PatrolStartedEvent>().Publish();

            if (_eventAggregator.GetEvent<CoordinatesReceivedEvent>().Contains(SendInitialPatrolSignal))
                _eventAggregator.GetEvent<CoordinatesReceivedEvent>().Unsubscribe(SendInitialPatrolSignal);
        }

        private void SendLogs(string message, int Qos = 1)
        {
            if (!CanSendData())
            {
                Log.Warning("Cannot send telemetry. Device settings are missing. (SerialNumber={serialNumber}, Stage={stage})", Settings.Default.DeviceSerialNumber, Settings.Default.Stage);
                return;
            }

            try
            {
                _iotData.Send($"pa/data/{Settings.Default.Stage}/{Settings.Default.DeviceSerialNumber}/logs", message, Qos);
                _eventAggregator.GetEvent<NetworkConnectedEvent>().Publish();
            }
            catch (AmazonServiceException ex) when (ex.Message.Contains("NameResolutionFailure"))
            {
                Log.Debug("Failed to send message: PC is offline");
                _eventAggregator.GetEvent<NoNetworkConnectionEvent>().Publish();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send log message");
            }
        }

        private async Task SendLogsAsync(string message, int Qos = 1)
        {
            if (!CanSendData())
            {
                Log.Warning("Cannot send telemetry. Device settings are missing. (SerialNumber={serialNumber}, Stage={stage})", Settings.Default.DeviceSerialNumber, Settings.Default.Stage);
                return;
            }

            try
            {
                await _iotData.SendAsync($"pa/data/{Settings.Default.Stage}/{Settings.Default.DeviceSerialNumber}/logs", message, Qos);
                _eventAggregator.GetEvent<NetworkConnectedEvent>().Publish();
            }
            catch (AmazonServiceException ex) when (ex.Message.Contains("NameResolutionFailure"))
            {
                Log.Debug("Failed to send message: PC is offline");
                _eventAggregator.GetEvent<NoNetworkConnectionEvent>().Publish();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send log message");
            }
        }

        private void SendPatrolSignal()
        {
            Log.Debug("Sending {mode} signal", _modeService.PatrolMode.Message);
            SendTelemetry(_modeService.PatrolMode, _mostRecentCoordinates);
        }

        private void SendTelemetry(Coordinates coordinates)
        {
            if (_modeService.ActiveModes.Count >= 1)
            {
                foreach (var mode in _modeService.ActiveModes)
                {
                    Log.Verbose("Sending {mode} signal", mode.Message);
                    SendTelemetry(mode, coordinates);
                }
            }
            else
            {
                SendPatrolSignal();
            }
        }

        private void SendTelemetry(Mode mode, Coordinates coordinates, bool sendEndSignal = false)
        {
            var payload = SignalBuilder.Build(mode, coordinates, sendEndSignal);
            _lastSentCoordinates = coordinates;
            SendTelemetry(payload);
        }

        private void SendTelemetry(string message, int Qos = 1)
        {
            if (!CanSendData())
            {
                Log.Warning("Cannot send telemetry. Device settings are missing. (SerialNumber={serialNumber}, Stage={stage})", Settings.Default.DeviceSerialNumber, Settings.Default.Stage);
                return;
            }

            try
            {
                _iotData.Send($"pa/data/{Settings.Default.Stage}/{Settings.Default.DeviceSerialNumber}/telemetry", message, Qos);
                LastTelemetrySendTime = DateTime.UtcNow;
                _eventAggregator.GetEvent<NetworkConnectedEvent>().Publish();
            }
            catch (AmazonServiceException ex) when (ex.Message.Contains("NameResolutionFailure"))
            {
                Log.Debug("Failed to send message: PC is offline");
                _eventAggregator.GetEvent<NoNetworkConnectionEvent>().Publish();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send telemetry");
            }
        }

        private async Task SendTelemetryAsync(string message, int Qos = 1)
        {
            if (!CanSendData())
            {
                Log.Warning("Cannot send telemetry. Device settings are missing. (SerialNumber={serialNumber}, Stage={stage})", Settings.Default.DeviceSerialNumber, Settings.Default.Stage);
                return;
            }

            try
            {
                await _iotData.SendAsync($"pa/data/{Settings.Default.Stage}/{Settings.Default.DeviceSerialNumber}/telemetry", message, Qos);
                LastTelemetrySendTime = DateTime.UtcNow;
                _eventAggregator.GetEvent<NetworkConnectedEvent>().Publish();
            }
            catch (AmazonServiceException ex) when (ex.Message.Contains("NameResolutionFailure"))
            {
                Log.Debug("Failed to send message: PC is offline");
                _eventAggregator.GetEvent<NoNetworkConnectionEvent>().Publish();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send telemetry");
            }
        }

        private void SendTelemetryIfNoneSentWithinThreshold(object sender, ElapsedEventArgs e)
        {
            // Check for min time on LastTelemetrySendTime, this will be the default. The timer
            // fires as soon as it's created, at which point we dont' have any coordinates yet
            if (LastTelemetrySendTime != DateTime.MinValue && DateTime.UtcNow.Subtract(LastTelemetrySendTime).TotalSeconds > IDLETELEMETRYINTERVALINSECONDS && _deviceService.IsDeviceConnected)
            {
                Log.Verbose("Telemetry has not been sent in more than {threshold} seconds. Sending now", IDLETELEMETRYINTERVALINSECONDS, _modeService.PatrolMode.Message);

                // If the vehicle is stationary, send the last reported location and speed to reduce
                // zig-zagging when mapping
                if (VehicleStatus == VehicleStatus.Stationary)
                    SendTelemetry(new Coordinates
                    {
                        Bearing = 0,
                        Latitude = _lastSentCoordinates.Latitude,
                        Longitude = _lastSentCoordinates.Longitude,
                        Speed = 0,
                        Time = DateTime.UtcNow
                    });
                else
                    SendTelemetry(_mostRecentCoordinates);
            }

            if (!_deviceService.IsDeviceConnected && IsSendingTelemtry())
            {
                // The device is disconnected; stop the timer
                StopSendingTelemetry("Device is disconnected");
            }
        }

        private bool ShouldSendTelemetry(Coordinates currentLocation)
        {
            // If the vehicle is in constant motion, e.g. traveling on the highway at 60 mph, send
            // telemetry on a set interval. If the vehicle was moving quickly, but just now stopped,
            // then we need to send a quicker burst to update the currently stopped location If the
            // vehicle was stationary, but is now moving quickly, we need to start sending data up
            // more quickly

            if (VehicleStatus == VehicleStatus.Moving)
            {
                // If speed is less than 15 MPH, send data if none has been sent in the last 4 seconds
                if (currentLocation.Speed <= 15)
                {
                    return DateTime.UtcNow.Subtract(LastTelemetrySendTime).TotalSeconds <= 4;
                }

                // If speed is greater than 15 MPH and less than or equal to 25 MPH, send data every
                // 25 meters
                if (currentLocation.Speed > 15 && currentLocation.Speed <= 25)

                    // Send data every 25 meters (between 2 - 3 seconds)
                    return _gpsService.DistanceBetween(currentLocation, _lastSentCoordinates) >= 0.015534;

                // If speed is greater than 25 MPH and less than or equal to 45 MPH
                if (currentLocation.Speed > 25 && currentLocation.Speed <= 45)

                    // Send data every 50 meters (between 2 - 4 seconds)
                    return _gpsService.DistanceBetween(currentLocation, _lastSentCoordinates) >= 0.031069;

                // If speed is greater than 45 and less than or equal to 75
                if (currentLocation.Speed > 45 && currentLocation.Speed <= 75)

                    // Send data every 75 meters (between 2 and 4 seconds)
                    return _gpsService.DistanceBetween(currentLocation, _lastSentCoordinates) >= 0.046603;

                if (currentLocation.Speed > 75 && currentLocation.Speed <= 90)

                    // Send data every 100 meters (between 2 and 3 seconds)
                    return _gpsService.DistanceBetween(currentLocation, _lastSentCoordinates) >= 0.062137;

                // If the speed is greater than 90 MPH, send data every 125 meters
                return _gpsService.DistanceBetween(currentLocation, _lastSentCoordinates) >= 0.077671;
            }
            else
            {
                if (_lastSentCoordinates.Speed > 5)
                {
                    // If we were previously travelling more than 5MPH, but have come to a hard
                    // stop, send data up
                    return true;
                }

                return false;
            }
        }

        private bool SignificantBearingChanged(Coordinates currentLocation, Coordinates startingPoint, out double bearingChange)
        {
            bearingChange = 0.00;
            if (startingPoint != null && currentLocation.HasLocationData() && startingPoint.HasLocationData())
                bearingChange = Math.Abs(startingPoint.Bearing - currentLocation.Bearing);
            return bearingChange > SIGNIFICANTTHRESHOLDFORBEARINGCHANGE;
        }

        private bool SignificantDistanceTraveled(Coordinates currentLocation, Coordinates startingPoint, out double distance)
        {
            distance = 0.00;
            if (startingPoint != null && currentLocation.HasLocationData() && startingPoint.HasLocationData())
                distance = _gpsService.DistanceBetween(startingPoint, currentLocation);
            return distance > DISTANCETHRESHOLDFORMOVINGINMILES;
        }

        #endregion Methods
    }
}