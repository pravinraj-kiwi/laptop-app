using Newtonsoft.Json;
using Prism.Events;
using PursuitAlert.Client.Properties;
using PursuitAlert.Client.Services.Device.Events;
using PursuitAlert.Client.Services.Device.Events.EventPayloads;
using PursuitAlert.Client.Services.Modes.Events;
using PursuitAlert.Client.Services.Modes.Events.EventPayloads;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace PursuitAlert.Client.Services.Modes
{
    public class ModeService : IModeService
    {
        #region Properties

        public List<Mode> ActiveModes { get; private set; }

        public Timer DelayedModeCountdownTimer { get; private set; }

        public int DelayedModeSecondsRemaining { get; private set; }

        public bool IsListening
        {
            get
            {
                if (_eventAggregator == null)
                    return false;
                return _eventAggregator.GetEvent<ButtonPressedEvent>().Contains(HandleButtonPress);
            }
        }

        public List<Mode> ModeConfiguration { get; private set; }

        public Mode PatrolMode { get; private set; }

        public Mode PowerOffMode { get; private set; }

        #endregion Properties

        #region Fields

        private const string PatrolModeKey = "PatrolMode";

        private const string PinDropModeKey = "PinDrop";

        private const string PowerOffModeKey = "PowerOff";

        private readonly IEventAggregator _eventAggregator;

        private readonly object ModeLock = new object();

        #endregion Fields

        #region Constructors

        public ModeService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            ActiveModes = new List<Mode>();

            //Set configuration directory path
            var configurationFilePath = Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath);

            // Create the configuration directory if it doesn't exist
            var configurationDirectory = Path.GetDirectoryName(configurationFilePath);
            if (!Directory.Exists(configurationDirectory))
            {
                Log.Verbose("Creating configuration directory {0}", configurationDirectory);
                Directory.CreateDirectory(configurationDirectory);
                Log.Debug("Configuration directory created {0}", configurationDirectory);
            }

            // Create default button configuration
            if (!File.Exists(configurationFilePath))
            {
                Log.Verbose("Creating default button configuration file {0}", configurationFilePath);
                var buttonConfigurationJson = Encoding.Default.GetString(DefaultConfiguration.defaultConfig);
                File.WriteAllText(configurationFilePath, buttonConfigurationJson);
                Log.Debug("Default button configuration file created {0}", configurationFilePath);
            }

            // Get the initial modes from the defaultConfig file and load them here
            Log.Verbose("Loading configuration from {0}", Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath));
            var configJson = File.ReadAllText(Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath));
            var config = JsonConvert.DeserializeObject<JurisdictionConfiguration>(configJson);
            Log.Debug("Loaded button configuration from {0}", Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath));

            if (config == null)
                throw new InvalidOperationException("Loaded configuration is null");

            SetConfiguration(config.Modes);
        }

        #endregion Constructors

        #region Methods

        public void BeginDelayedModeCountdown(Mode mode)
        {
            // Any button presses that come in during the countdown should be processed as an
            // attempt to cancel the mode, so unmap the event handler to process button presses as
            // mode engagements/disengagements, and map an event handler to cancel the countdown
            if (_eventAggregator.GetEvent<ButtonPressedEvent>().Contains(HandleButtonPress))
                _eventAggregator.GetEvent<ButtonPressedEvent>().Unsubscribe(HandleButtonPress);

            if (!_eventAggregator.GetEvent<ButtonPressedEvent>().Contains(CancelDelayedModeEngaging))
                _eventAggregator.GetEvent<ButtonPressedEvent>().Subscribe(CancelDelayedModeEngaging);

            // Publish the event to let the UI know that the countdown has started
            _eventAggregator.GetEvent<DelayedModeEngagedEvent>().Publish(mode);

            DelayedModeSecondsRemaining = mode.IncidentDelay;
            DelayedModeCountdownTimer = new Timer(1000);
            DelayedModeCountdownTimer.Elapsed += (s, e) => Countdown(mode);
            DelayedModeCountdownTimer.Enabled = true;
        }

        public void ListenForEvents()
        {
            if (!_eventAggregator.GetEvent<ButtonPressedEvent>().Contains(HandleButtonPress))
                _eventAggregator.GetEvent<ButtonPressedEvent>().Subscribe(HandleButtonPress);
        }

        public void SetConfiguration(List<Mode> modes)
        {
            Log.Verbose("Setting switch mapping: {0}", JsonConvert.SerializeObject(modes));
            lock (ModeLock)
            {
                ModeConfiguration = modes;
                PowerOffMode = modes.FirstOrDefault(m => m.Name.Equals(PowerOffModeKey));
                PatrolMode = modes.FirstOrDefault(m => m.Name.Equals(PatrolModeKey));
            }
            Log.Debug("Switch mapping set: {0}", JsonConvert.SerializeObject(modes));
        }

        public void StopListeningForEvents()
        {
            if (_eventAggregator.GetEvent<ButtonPressedEvent>().Contains(HandleButtonPress))
                _eventAggregator.GetEvent<ButtonPressedEvent>().Unsubscribe(HandleButtonPress);
        }

        private void CancelDelayedModeEngaging(int buttonNumber)
        {
            // Determine which mode is mapped to the button that was pressed
            Mode modeMappedToButton = null;
            lock (ModeLock)
                modeMappedToButton = ModeConfiguration.FirstOrDefault(m => m.ButtonPosition == buttonNumber);

            DelayedModeSecondsRemaining = 0;
            DelayedModeCountdownTimer.Elapsed -= (s, e) => Countdown(modeMappedToButton);
            DelayedModeCountdownTimer.Stop();
            DelayedModeCountdownTimer.Enabled = false;
            DelayedModeCountdownTimer.Dispose();
            DelayedModeCountdownTimer = null;

            // Publish the event to tell the UI that the countdown has been canceled
            _eventAggregator.GetEvent<DelayedModeCanceledEvent>().Publish(modeMappedToButton);

            // Return the event handler for button press events back to engaging/disengaging modes
            if (_eventAggregator.GetEvent<ButtonPressedEvent>().Contains(CancelDelayedModeEngaging))
                _eventAggregator.GetEvent<ButtonPressedEvent>().Unsubscribe(CancelDelayedModeEngaging);

            if (!_eventAggregator.GetEvent<ButtonPressedEvent>().Contains(HandleButtonPress))
                _eventAggregator.GetEvent<ButtonPressedEvent>().Subscribe(HandleButtonPress);
        }

        private void Countdown(Mode mode)
        {
            DelayedModeSecondsRemaining--;
            _eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Publish(new DelayedModeTimerTickEventPayload { SecondsRemaining = DelayedModeSecondsRemaining, TotalSeconds = mode.IncidentDelay, Mode = mode });

            if (DelayedModeSecondsRemaining == 0)
            {
                Log.Debug("Engaging mode {modeName} mode", mode.Message);
                ActiveModes.Add(mode);
                var modeChange = new ModeChangeEventPayload
                {
                    ChangeType = ModeChangeType.Engaged,
                    NewMode = mode
                };
                _eventAggregator.GetEvent<ModeChangeEvent>().Publish(modeChange);

                // Return the event handler for button press events back to engaging/disengaging modes
                if (_eventAggregator.GetEvent<ButtonPressedEvent>().Contains(CancelDelayedModeEngaging))
                    _eventAggregator.GetEvent<ButtonPressedEvent>().Unsubscribe(CancelDelayedModeEngaging);

                if (!_eventAggregator.GetEvent<ButtonPressedEvent>().Contains(HandleButtonPress))
                    _eventAggregator.GetEvent<ButtonPressedEvent>().Subscribe(HandleButtonPress);

                DelayedModeSecondsRemaining = 0;
                DelayedModeCountdownTimer.Elapsed -= (s, e) => Countdown(mode);
                DelayedModeCountdownTimer.Stop();
                DelayedModeCountdownTimer.Enabled = false;
                DelayedModeCountdownTimer.Dispose();
                DelayedModeCountdownTimer = null;
            }
        }

        private void HandleButtonPress(int buttonNumber)
        {
            // Determine which mode is mapped to the button that was pressed
            Mode modeMappedToButton = null;
            lock (ModeLock)
                modeMappedToButton = ModeConfiguration.FirstOrDefault(m => m.ButtonPosition == buttonNumber);

            // If there is no mode mapped to the button that was pressed, return
            if (modeMappedToButton == null)
            {
                Log.Warning("Button {buttonNumber} was pressed, but there is no mode mapped to that button", buttonNumber);
                _eventAggregator.GetEvent<UnmappedButtonPressedEvent>().Publish(new UnmappedButtonPressedEventPayload { ButtonPressed = buttonNumber, ConfiguredModes = ModeConfiguration });
                return;
            }

            // Handle a pin drop
            if (modeMappedToButton.Name == PinDropModeKey)
            {
                Log.Debug("Pin dropped");
                _eventAggregator.GetEvent<PinDroppedEvent>().Publish(modeMappedToButton);
                return;
            }

            // Handle mode toggle
            ModeChangeEventPayload modeChange = null;
            if (ActiveModes.Contains(modeMappedToButton))
            {
                Log.Debug("Disengaging {modeName} mode", modeMappedToButton.Message);
                ActiveModes.Remove(modeMappedToButton);
                modeChange = new ModeChangeEventPayload
                {
                    ChangeType = ModeChangeType.Disengaged,
                    NewMode = modeMappedToButton
                };
            }
            else
            {
                // Handle a delayed mode
                if (modeMappedToButton.IncidentDelay != 0)
                {
                    BeginDelayedModeCountdown(modeMappedToButton);
                    return;
                }

                Log.Debug("Engaging mode {modeName} mode", modeMappedToButton.Message);
                ActiveModes.Add(modeMappedToButton);
                modeChange = new ModeChangeEventPayload
                {
                    ChangeType = ModeChangeType.Engaged,
                    NewMode = modeMappedToButton
                };
            }

            _eventAggregator.GetEvent<ModeChangeEvent>().Publish(modeChange);
        }

        #endregion Methods
    }
}