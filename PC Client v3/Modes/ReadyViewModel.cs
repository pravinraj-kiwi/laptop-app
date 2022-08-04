using Newtonsoft.Json;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using PursuitAlert.Client.Old.Properties;
using PursuitAlert.Domain.Audio.Services;
using PursuitAlert.Domain.Configuration.Events;
using PursuitAlert.Domain.Configuration.Models;
using PursuitAlert.Domain.Device.Events;
using PursuitAlert.Domain.Modes.Events;
using PursuitAlert.Domain.Modes.Models;
using PursuitAlert.Domain.Modes.Services;
using PursuitAlert.Domain.Publishing.Services;
using Serilog;
using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace PursuitAlert.Client.Old.Modes
{
    public class ReadyViewModel : BindableBase, INavigationAware
    {
        #region Properties

        public bool Animate
        {
            get { return _animate; }
            set { SetProperty(ref _animate, value); }
        }

        public string Heading
        {
            get { return _heading; }
            set { SetProperty(ref _heading, value); }
        }

        public bool KeepAlive => true;

        public SolidColorBrush LogoColor
        {
            get { return _logoColor; }
            set { SetProperty(ref _logoColor, value); }
        }

        public string Subheading
        {
            get { return _subheading; }
            set { SetProperty(ref _subheading, value); }
        }

        #endregion Properties

        #region Fields

        private const string ActiveHeading = "ACTIVE";

        private const string EmergencyRedBrush = "EmergencyRedBrush";

        private const string LightBrush = "LightBrush";

        private const string MultipleActiveSubheading = "Multiple events";

        private const string ReadyHeading = "READY";

        private const string ReadySubheading = "System ready";

        private readonly IDialogService _dialogService;

        private readonly IEventAggregator _eventAggregator;

        private readonly IModeService _modeService;

        private readonly IRegionManager _navigationService;

        private readonly IPublishingService _publishingService;

        private readonly ISoundPlayerService _soundPlayer;

        private bool _animate;

        private string _heading;

        private SolidColorBrush _logoColor;

        private string _subheading;

        private DispatcherTimer CountdownTimer;

        private bool DisconnectionWarningSoundPlayed;

        #endregion Fields

        #region Constructors

        public ReadyViewModel(IPublishingService publishingService, IModeService modeService, IDialogService dialogService, IRegionManager regionManager, ISoundPlayerService soundPlayer, IEventAggregator eventAggreator)
        {
            _publishingService = publishingService;
            _modeService = modeService;
            _dialogService = dialogService;
            _navigationService = regionManager;
            _eventAggregator = eventAggreator;
            _soundPlayer = soundPlayer;

            SetEventSubscriptions();

            Heading = ReadyHeading;
            Subheading = ReadySubheading;
            LogoColor = (SolidColorBrush)App.Current.FindResource(LightBrush);
        }

        #endregion Constructors

        #region Methods

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }

        public void OnNavigatedTo(NavigationContext navigationContext) => _soundPlayer.PlaySuccessSound();

        private void HandleDeviceConnected(SerialPort port)
        {
            DisconnectionWarningSoundPlayed = false;

            // Start listening for disconnections after connection
            if (!_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleDeviceDisconnection))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleDeviceDisconnection);

            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceConnected))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceConnected);
        }

        private void HandleDeviceDisconnection()
        {
            if (!DisconnectionWarningSoundPlayed)
            {
                _soundPlayer.PlayWarningSound();
                DisconnectionWarningSoundPlayed = true;
            }

            // ! This fixes an NRE that was happing on application shutdown. If the app is shutting
            // down, don't try to navigate
            if (App.Current != null)
                try
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        // Go back to the initializing view
                        _navigationService.RequestNavigate(ContentRegionNames.ModeRegion, nameof(InitializingView));
                    });
                }
                catch (TaskCanceledException)
                {
                    // This happens when the app is shutting down.
                    Log.Debug("Attempted to navigate to the Initializing View, but the app is shutting down.");
                }

            // Unsubscribe from the disconnection event. If the device is disconnected, we don't
            // need to hear about it again. The DeviceConnected event will re-hookup the
            // disconnected event
            if (_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleDeviceDisconnection))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(HandleDeviceDisconnection);

            if (!_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceConnected))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceConnected);
        }

        private void HandleModeChange(ModeChangeEventArgs modeChange)
        {
            Log.Verbose("Mode change event args: {0}", JsonConvert.SerializeObject(modeChange));
            if (modeChange.ChangeType == ModeChangeType.ModeEngaged && modeChange.ActivatedModes.Count > 1)
            {
                var multipleModeString = string.Join(", ", modeChange.ActivatedModes.Select(m => m.Message)).TrimEnd(new char[] { ',', ' ' });

                if (multipleModeString.Length > 30)
                    multipleModeString = MultipleActiveSubheading;

                SetMode(ActiveHeading, multipleModeString, modeChange.NewMode.Animate, modeChange.NewMode.ColorName);

                if (modeChange.NewMode.PlaySound)
                    _soundPlayer.PlayModeEngagedSound();
            }
            else if (modeChange.ChangeType == ModeChangeType.ModeEngaged)
            {
                SetMode(ActiveHeading, modeChange.NewMode.Message, modeChange.NewMode.Animate, modeChange.NewMode.ColorName);

                if (modeChange.NewMode.PlaySound)
                    _soundPlayer.PlayModeEngagedSound();
            }
            else if (modeChange.ChangeType == ModeChangeType.ModeDisengaged && modeChange.ActivatedModes.Count == 0)
            {
                SetMode(ReadyHeading, ReadySubheading, false, LightBrush);

                if (modeChange.OriginalMode != null && modeChange.OriginalMode.PlaySound)
                    _soundPlayer.PlayModeDisengagedSound();
            }
            else if (modeChange.ChangeType == ModeChangeType.ModeDisengaged && modeChange.ActivatedModes.Count > 1)
            {
                SetMode(ActiveHeading, MultipleActiveSubheading, modeChange.NewMode.Animate, modeChange.NewMode.ColorName);

                if (modeChange.OriginalMode != null && modeChange.OriginalMode.PlaySound)
                    _soundPlayer.PlayModeDisengagedSound();
            }
            else if (modeChange.ChangeType == ModeChangeType.ModeDisengaged)
            {
                SetMode(ActiveHeading, modeChange.NewMode.Message, modeChange.NewMode.Animate, modeChange.NewMode.ColorName);

                if (modeChange.OriginalMode != null && modeChange.OriginalMode.PlaySound)
                    _soundPlayer.PlayModeDisengagedSound();
            }
        }

        private void HandleNewConfigurationReceived(DeviceConfigurationRetrievedEventArgs config)
        {
            // Update the date the last configuration was retrieved
            Log.Debug("Updating configuration date in settings from {0} to {1}", Settings.Default.ConfigurationLastUpdated.ToString(), config.RetrievedAt.ToString());
            Settings.Default.ConfigurationLastUpdated = config.RetrievedAt;
            Settings.Default.Save();
            Log.Verbose("Configuration date setting updated");

            // Set the config file to the new data
            Log.Debug("Writing new configuration to {0}", Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath));
            try
            {
                var configJson = JsonConvert.SerializeObject(config);
                File.WriteAllText(Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath), configJson);
                Log.Verbose("New configuration written to {0}", Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to write new configuration to {0}", Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath));
            }
        }

        private void HandlePinDrop(Mode pinDropped)
        {
            // The PinDrop animation is handled in ClientWindow.xaml.cs
            _soundPlayer.PlayPinDropSound();
        }

        private void SetEventSubscriptions()
        {
            _eventAggregator.GetEvent<ModeChangeEvent>().Subscribe(HandleModeChange);
            _eventAggregator.GetEvent<DelayedModeCountdownTimerTick>().Subscribe(mode => Task.Run(() => _soundPlayer.PlayWarningSound()).ConfigureAwait(false));
            _eventAggregator.GetEvent<DelayedModeCancelRequestedEvent>().Subscribe(mode => Task.Run(() => _soundPlayer.PlayModeDisengagedSound()));
            _eventAggregator.GetEvent<PinDroppedEvent>().Subscribe(HandlePinDrop);
            _eventAggregator.GetEvent<UnmappedSwitchActivatedEvent>().Subscribe((switchNumber) => _soundPlayer.PlayWarningSound());

            _eventAggregator.GetEvent<DeviceConfigurationUpdatedEvent>().Subscribe(HandleNewConfigurationReceived);

            _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceConnected);
            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleDeviceDisconnection);
        }

        private void SetMode(string heading, string subheading, bool animate, string logoColorName)
        {
            Heading = heading;
            Subheading = subheading;
            Animate = animate;
            LogoColor = (SolidColorBrush)App.Current.FindResource(logoColorName);
        }

        #endregion Methods
    }
}