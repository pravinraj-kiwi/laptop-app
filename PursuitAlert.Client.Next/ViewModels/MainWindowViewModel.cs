using FontAwesome.WPF;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using PursuitAlert.Client.Dialogs.SettingsDialog;
using PursuitAlert.Client.Events;
using PursuitAlert.Client.Events.Payloads;
using PursuitAlert.Client.Infrastructure.API;
using PursuitAlert.Client.Infrastructure.Email;
using PursuitAlert.Client.Infrastructure.IoTManagement;
using PursuitAlert.Client.Properties;
using PursuitAlert.Client.Resources.Colors;
using PursuitAlert.Client.Services.Device;
using PursuitAlert.Client.Services.Device.Connection;
using PursuitAlert.Client.Services.Device.Events;
using PursuitAlert.Client.Services.Device.Events.EventPayloads;
using PursuitAlert.Client.Services.GPS;
using PursuitAlert.Client.Services.GPS.Events;
using PursuitAlert.Client.Services.Modes;
using PursuitAlert.Client.Services.Modes.Events;
using PursuitAlert.Client.Services.Modes.Events.EventPayloads;
using PursuitAlert.Client.Services.Sounds;
using PursuitAlert.Client.Services.Telemetry;
using PursuitAlert.Client.Services.Telemetry.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PursuitAlert.Client.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        #region Properties

        public bool AnimateLogo
        {
            get => _animateLogo;
            set => SetProperty(ref _animateLogo, value);
        }

        public int AuthenticationAttempt { get; private set; } = 1;

        public SolidColorBrush CountdownBackgroundColor
        {
            get => _countdownBackgroundColor;
            set => SetProperty(ref _countdownBackgroundColor, value);
        }

        public string CountdownTimerMessage
        {
            get => _countdownTimerMessage;
            set => SetProperty(ref _countdownTimerMessage, value);
        }

        public int CountdownTimerSeconds
        {
            get => _countdownTimerSeconds;
            set => SetProperty(ref _countdownTimerSeconds, value);
        }

        public bool DebugMode => Settings.Default.DebugMode;

        public DelegateCommand EmailLogsCommand { get; }

        public string GPSStateIcon
        {
            get => _gpsStateIcon;
            set => SetProperty(ref _gpsStateIcon, value);
        }

        public SolidColorBrush GPSStateIconForeground
        {
            get => _gpsStateIconForeground;
            set => SetProperty(ref _gpsStateIconForeground, value);
        }

        public string GPSStateMessage
        {
            get => _gpsStateMessage;
            set => SetProperty(ref _gpsStateMessage, value);
        }

        public string Heading
        {
            get => _heading;
            set => SetProperty(ref _heading, value);
        }

        public DelegateCommand HideCommand { get; private set; }

        public bool IsDebugMode => Settings.Default.DebugMode;

        public SolidColorBrush LogoColor
        {
            get => _logoColor;
            set => SetProperty(ref _logoColor, value);
        }

        public DelegateCommand OpenSettingsCommand { get; }

        public DelegateCommand OpenWebPortalCommand { get; }

        public string PatrolStateIcon
        {
            get => _patrolStateIcon;
            set => SetProperty(ref _patrolStateIcon, value);
        }

        public SolidColorBrush PatrolStateIconForeground
        {
            get => _patrolStateIconForeground;
            set => SetProperty(ref _patrolStateIconForeground, value);
        }

        public string PatrolStateMessage
        {
            get => _patrolStateMessage;
            set => SetProperty(ref _patrolStateMessage, value);
        }

        public bool ShowCountdownTimer
        {
            get => _showCountdownTimer;
            set => SetProperty(ref _showCountdownTimer, value);
        }

        public string StateIcon
        {
            get => _stateIcon;
            set => SetProperty(ref _stateIcon, value);
        }

        public SolidColorBrush StateIconForeground
        {
            get => _stateIconForeground;
            set => SetProperty(ref _stateIconForeground, value);
        }

        public string StateMessage
        {
            get => _stateMessage;
            set => SetProperty(ref _stateMessage, value);
        }

        public string Subheading
        {
            get => _subheading;
            set => SetProperty(ref _subheading, value);
        }

        public string Title => Properties.Resources.ApplicationTitle;

        public string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        #endregion Properties

        #region Fields

        private const int AUTHENTICATION_RETRY_TIMEOUT = 3000;

        private readonly IAPIService _api;

        private readonly IDeviceService _deviceService;

        private readonly IDialogService _dialogService;

        private readonly IEmailService _emailService;

        private readonly IEventAggregator _eventAggregator;

        private readonly IIoTManagementService _iotManagement;

        private readonly IModeService _modeService;

        private readonly ISoundPlayerService _soundPlayerService;

        private readonly ITelemetryService _telemetryService;

        private bool _animateLogo;

        private SolidColorBrush _countdownBackgroundColor;

        private string _countdownTimerMessage;

        private int _countdownTimerSeconds;

        private string _gpsStateIcon;

        private SolidColorBrush _gpsStateIconForeground;

        private string _gpsStateMessage;

        private string _heading;

        private SolidColorBrush _logoColor;

        private string _patrolStateIcon;

        private SolidColorBrush _patrolStateIconForeground;

        private string _patrolStateMessage;

        private bool _showCountdownTimer;

        private string _stateIcon;

        private SolidColorBrush _stateIconForeground;

        private string _stateMessage;

        private string _subheading;

        private NotifyIcon trayIcon = new NotifyIcon();

        #endregion Fields

        #region Constructors

        public MainWindowViewModel(IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDeviceService deviceService,
            IAPIService apiService,
            IEmailService emailService,
            IModeService modeService,
            ITelemetryService telemetryService,
            IIoTManagementService iotManagement,
            ISoundPlayerService soundPlayerService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _deviceService = deviceService;
            _api = apiService;
            _emailService = emailService;
            _modeService = modeService;
            _telemetryService = telemetryService;
            _iotManagement = iotManagement;
            _soundPlayerService = soundPlayerService;

            OpenWebPortalCommand = new DelegateCommand(OpenWebPortal);
            HideCommand = new DelegateCommand(HideWindow);
            EmailLogsCommand = new DelegateCommand(EmailLogs);
            OpenSettingsCommand = new DelegateCommand(OpenSettings);

            SetTaskbarIcon();

            SetInitialState();

            // Start everything up
            if (!_eventAggregator.GetEvent<AuthenticationExtendedEvent>().Contains(HandleAuthenticationExtended))
                _eventAggregator.GetEvent<AuthenticationExtendedEvent>().Subscribe(HandleAuthenticationExtended);

            Task.WhenAll(_api.Authenticate(throwOnTimeout: true), _telemetryService.Initialize())
                .ContinueWith(init =>
                {
                    // We don't need this anymore since we're done authenticating
                    if (_eventAggregator.GetEvent<AuthenticationExtendedEvent>().Contains(HandleAuthenticationExtended))
                        _eventAggregator.GetEvent<AuthenticationExtendedEvent>().Unsubscribe(HandleAuthenticationExtended);

                    if (init.IsFaulted)
                    {
                        SetContent(States.AuthenticationFailure, BrushNames.Muted, States.CantReachServer, false);
                        RetryAuthentication();
                        _soundPlayerService.PlayWarningSound();
                        return;
                    }

                    SetContent(States.Initializing, BrushNames.Muted, States.SearchingForDevice, false);
                    Initialize();
                    _soundPlayerService.PlaySuccessSound();
                });
        }

        #endregion Constructors

        #region Methods

        public void HideCountdownTimer()
        {
            ShowCountdownTimer = false;
            CountdownTimerMessage = string.Empty;
            CountdownBackgroundColor = (SolidColorBrush)App.Current.FindResource(BrushNames.Muted);
            CountdownTimerSeconds = 0;
        }

        public void SetContent(string heading, string color, string subheading = "", bool animateLogo = false)
        {
            Heading = heading;
            if (!string.IsNullOrWhiteSpace(subheading))
                Subheading = subheading;
            LogoColor = (SolidColorBrush)App.Current.FindResource(color);
            AnimateLogo = animateLogo;
        }

        public void SetCountdownTimer(string message, string color, int totalSeconds)
        {
            ShowCountdownTimer = true;
            CountdownTimerMessage = message;
            CountdownBackgroundColor = (SolidColorBrush)App.Current.FindResource(color);
            CountdownTimerSeconds = totalSeconds;
            _soundPlayerService.PlayWarningSound();
        }

        private void CancelDelayedModeCountdown(Mode mode)
        {
            if (_eventAggregator.GetEvent<DelayedModeCanceledEvent>().Contains(CancelDelayedModeCountdown))
                _eventAggregator.GetEvent<DelayedModeCanceledEvent>().Unsubscribe(CancelDelayedModeCountdown);

            if (_eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Contains(CountdownTimerTick))
                _eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Unsubscribe(CountdownTimerTick);

            HideCountdownTimer();
        }

        private void CountdownTimerTick(DelayedModeTimerTickEventPayload tick)
        {
            CountdownTimerSeconds = tick.SecondsRemaining;
            _soundPlayerService.PlayWarningSound();
        }

        private void EmailLogs()
        {
            _emailService.SendEmailTodaysLogs();
        }

        private void HandleAuthenticationExtended(UIMessage message)
        {
            Subheading = message.Brief;
        }

        private void HandleDeviceConnected()
        {
            Initialize();
            SetDeviceConnectedState();
        }

        private void HandleDeviceDisconnected()
        {
            SetDeviceDisconnectedState();
        }

        private void HandleDeviceError(Exception obj)
        {
            SetState(States.DeviceError, FontAwesomeIcon.Exclamation, BrushNames.EmergencyRed);
            SetGPSState(States.GPSNoSignal, FontAwesomeIcon.Ban, BrushNames.Light);

            // Wait a few seconds and try again
            Log.Warning("Received a device error. Simulating a device disconnect and reconnect in 1/2 seconds");
            Task.Run(async () =>
            {
                await Task.Delay(500);
                Log.Information("Closing connection to device to recover after error.");
                _deviceService.CloseConnection("A device error occurred. Trying to close and reopen the connection to the device to recover.");
                Log.Information("Device connection closed. Reconnecting.");
                Initialize();
                Log.Information("Device reconnected successfully.");
            });
        }

        private void HandleDeviceFound()
        {
            SetContent(States.Initializing, BrushNames.Muted, States.DeviceFound, false);
            SetState(States.DeviceConnected, FontAwesomeIcon.Check, BrushNames.Light);

            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceFound))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceFound);

            if (_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleNoDeviceConnected))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(HandleNoDeviceConnected);
        }

        private void HandleGPSSignalAcquired()
        {
            SetGPSState(States.Connected, FontAwesomeIcon.Globe, BrushNames.Light);

            // Toggle the event handlers
            if (_eventAggregator.GetEvent<GPSSignalAcquiredEvent>().Contains(HandleGPSSignalAcquired))
                _eventAggregator.GetEvent<GPSSignalAcquiredEvent>().Unsubscribe(HandleGPSSignalAcquired);

            if (!_eventAggregator.GetEvent<GPSSignalLostEvent>().Contains(HandleGPSSignalLost))
                _eventAggregator.GetEvent<GPSSignalLostEvent>().Subscribe(HandleGPSSignalLost);
        }

        private void HandleGPSSignalLost()
        {
            SetGPSState(States.GPSSearching, FontAwesomeIcon.Globe, BrushNames.MoveOverYellow);

            // Toggle the event handlers
            if (_eventAggregator.GetEvent<GPSSignalLostEvent>().Contains(HandleGPSSignalLost))
                _eventAggregator.GetEvent<GPSSignalLostEvent>().Unsubscribe(HandleGPSSignalLost);

            if (!_eventAggregator.GetEvent<GPSSignalAcquiredEvent>().Contains(HandleGPSSignalAcquired))
                _eventAggregator.GetEvent<GPSSignalAcquiredEvent>().Subscribe(HandleGPSSignalAcquired);
        }

        private void HandleModeChange(ModeChangeEventPayload modeChange)
        {
            Log.Verbose("{modeName} mode engaged", modeChange.NewMode.Message);

            App.Current.Dispatcher.Invoke(() => ShowWindow(null, null));

            if (modeChange.ChangeType == ModeChangeType.Engaged)
            {
                ShowCountdownTimer = false;

                // Disable context menu items during an active event
                foreach (MenuItem menuItem in trayIcon.ContextMenu.MenuItems)
                    menuItem.Enabled = false;

                if (_modeService.ActiveModes.Count > 1)
                {
                    var multipleModeNames = string.Join(", ", _modeService.ActiveModes.Select(m => m.Message)).Trim().TrimEnd(',');
                    if (multipleModeNames.Length > 30)
                        multipleModeNames = States.MultipleActiveModes;

                    SetContent(States.Active, modeChange.NewMode.ColorName, multipleModeNames, modeChange.NewMode.Animate);
                }
                else
                {
                    SetContent(States.Active, modeChange.NewMode.ColorName, modeChange.NewMode.Message, modeChange.NewMode.Animate);
                }

                if (modeChange.NewMode.PlaySound)
                    _soundPlayerService.PlayModeEngagedSound();

                // Remove the logic to subscribe to delayed modes
                if (_eventAggregator.GetEvent<DelayedModeCanceledEvent>().Contains(CancelDelayedModeCountdown))
                    _eventAggregator.GetEvent<DelayedModeCanceledEvent>().Unsubscribe(CancelDelayedModeCountdown);

                if (_eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Contains(CountdownTimerTick))
                    _eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Unsubscribe(CountdownTimerTick);
            }
            else
            {
                if (_modeService.ActiveModes.Count >= 1)
                {
                    var multipleModeNames = string.Join(", ", _modeService.ActiveModes.Select(m => m.Message)).Trim().TrimEnd(',');
                    if (multipleModeNames.Length > 30)
                        multipleModeNames = States.MultipleActiveModes;

                    SetContent(States.Active, _modeService.ActiveModes.Last().ColorName, multipleModeNames, _modeService.ActiveModes.Last().Animate);
                }
                else
                {
                    SetContent(States.Ready, BrushNames.Light, States.SystemReady, false);
                }

                if (modeChange.NewMode.PlaySound)
                    _soundPlayerService.PlayModeDisengagedSound();

                // Re-enable the settings menu during an active event
                foreach (MenuItem menuItem in trayIcon.ContextMenu.MenuItems)
                    menuItem.Enabled = true;
            }
        }

        private void HandleNoDeviceConnected()
        {
            SetDeviceDisconnectedState();

            if (_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleNoDeviceConnected))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(HandleNoDeviceConnected);

            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceFound))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceFound);
        }

        private void HandlePatrolEnded()
        {
            SetPatrolState(States.NotPatrolling, FontAwesomeIcon.Circle, BrushNames.EmergencyRed);

            if (_eventAggregator.GetEvent<PatrolEndedEvent>().Contains(HandlePatrolEnded))
                _eventAggregator.GetEvent<PatrolEndedEvent>().Unsubscribe(HandlePatrolEnded);

            if (!_eventAggregator.GetEvent<PatrolStartedEvent>().Contains(HandlePatrolStarted))
                _eventAggregator.GetEvent<PatrolStartedEvent>().Subscribe(HandlePatrolStarted);
        }

        private void HandlePatrolStarted()
        {
            if (_telemetryService.VehicleStatus == VehicleStatus.Moving)
                SetPatrolState(States.OnPatrolMoving, FontAwesomeIcon.Circle, BrushNames.CriticalGreen);
            else
                SetPatrolState(States.OnPatrolStopped, FontAwesomeIcon.Circle, BrushNames.CriticalGreen);

            if (_eventAggregator.GetEvent<PatrolStartedEvent>().Contains(HandlePatrolStarted))
                _eventAggregator.GetEvent<PatrolStartedEvent>().Unsubscribe(HandlePatrolStarted);

            if (!_eventAggregator.GetEvent<PatrolEndedEvent>().Contains(HandlePatrolEnded))
                _eventAggregator.GetEvent<PatrolEndedEvent>().Subscribe(HandlePatrolEnded);
        }

        private void HandlePinDropped(Mode pinDropMode)
        {
            _soundPlayerService.PlayPinDropSound();
            App.Current.Dispatcher.Invoke(() =>
            {
                var pinDropAnimation = (Storyboard)App.Current.MainWindow.FindResource("PinDropped");
                pinDropAnimation.Begin();
            });
        }

        private void HandleUnmappedButtonPress(UnmappedButtonPressedEventPayload unmappedButton)
        {
            _soundPlayerService.PlayWarningSound();
        }

        private void HandleVehicleMoving()
        {
            SetPatrolState(States.OnPatrolMoving, FontAwesomeIcon.Road, BrushNames.CriticalGreen);
        }

        private void HandleVehicleStationary()
        {
            SetPatrolState(States.OnPatrolStopped, FontAwesomeIcon.Circle, BrushNames.CriticalGreen);
        }

        private void HideWindow()
        {
            System.Windows.Application.Current.MainWindow.Hide();
            System.Windows.Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        private void Initialize()
        {
            _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceFound);
            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleNoDeviceConnected);
            if (RequiredSettingsExist())
            {
                _deviceService.Initialize();
                SetReadyState();
            }
            else
            {
                SetContent(States.Initializing, BrushNames.Muted, States.WaitingForVehicle, false);
                App.Current.Dispatcher.Invoke(() =>
                {
                    _dialogService.ShowDialog(nameof(SettingsDialog), new DialogParameters("tab=vehicle_information"), result =>
                    {
                        if (RequiredSettingsExist())
                        {
                            Task.Run(() =>
                            {
                                SetContent(States.Initializing, BrushNames.Muted, States.SearchingForDevice, false);
                                _deviceService.Initialize();
                                SetReadyState();
                            });
                        }
                        else
                        {
                            Log.Information("Settings dialog has been closed without an OK response. Cannot continue without appropriate settings. Setting an event handler to trigger initialization once settings are updated.");

                            if (!_eventAggregator.GetEvent<DeviceSettingsChangedEvent>().Contains(InitializeDeviceAfterSettingsUpdate))
                                _eventAggregator.GetEvent<DeviceSettingsChangedEvent>().Subscribe(InitializeDeviceAfterSettingsUpdate);
                        }
                    });
                });
            }
        }

        private void InitializeDeviceAfterSettingsUpdate()
        {
            Initialize();

            if (_eventAggregator.GetEvent<DeviceSettingsChangedEvent>().Contains(InitializeDeviceAfterSettingsUpdate))
                _eventAggregator.GetEvent<DeviceSettingsChangedEvent>().Unsubscribe(InitializeDeviceAfterSettingsUpdate);
        }

        private void OpenSettings()
        {
            _dialogService.ShowDialog(nameof(SettingsDialog));
        }

        private void OpenWebPortal()
        {
            Process.Start(Settings.Default.PortalAddress);
        }

        private bool RequiredSettingsExist()
        {
            return OrganizationSettings.Default.Id != 0
                && !string.IsNullOrEmpty(OrganizationSettings.Default.Code)
                && !string.IsNullOrEmpty(OrganizationSettings.Default.DisplayName)
                && VehicleSettings.Default.Id != 0
                && !string.IsNullOrEmpty(VehicleSettings.Default.Officer)
                && !string.IsNullOrEmpty(VehicleSettings.Default.UnitId);
        }

        private void RetryAuthentication(int delay = AUTHENTICATION_RETRY_TIMEOUT)
        {
            ++AuthenticationAttempt;

            SetContent(States.Initializing, BrushNames.Muted, string.Format(States.AuthenticatingAttempt, AuthenticationAttempt), false);

            Task.WhenAll(_api.Authenticate(throwOnTimeout: true), _telemetryService.Initialize())
                    .ContinueWith(init =>
                {
                    if (init.IsFaulted)
                    {
                        Task.Delay(delay)
                            .ContinueWith(delayTask =>
                            {
                                var nextDelay = AUTHENTICATION_RETRY_TIMEOUT * Math.Min(AuthenticationAttempt, 10);

                                // Play the warning sound every fifth attempt
                                if (AuthenticationAttempt % 4 == 0)
                                    _soundPlayerService.PlayWarningSound();

                                RetryAuthentication(nextDelay);
                                return;
                            });
                        return;
                    }

                    SetContent(States.Initializing, BrushNames.Muted, States.SearchingForDevice, false);
                    Initialize();
                    _soundPlayerService.PlaySuccessSound();
                });
        }

        private void SetDeviceConnectedState()
        {
            SetContent(States.Ready, BrushNames.Light, States.SystemReady, false);
            SetState(States.DeviceConnected, FontAwesomeIcon.Check, BrushNames.Light);

            if (_telemetryService.VehicleStatus == VehicleStatus.Moving)
                SetPatrolState(States.OnPatrolMoving, FontAwesomeIcon.Road, BrushNames.CriticalGreen);
            else
                SetPatrolState(States.OnPatrolStopped, FontAwesomeIcon.Circle, BrushNames.CriticalGreen);

            if (!_deviceService.IsDeviceInitialized)
                _deviceService.Initialize();

            _modeService.ListenForEvents();
            _iotManagement.EnsureThingExists();
            _telemetryService.StartSendingTelemetry();

            if (!_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(SetDeviceDisconnectedState))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(SetDeviceDisconnectedState);

            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(SetDeviceConnectedState))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(SetDeviceConnectedState);

            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceFound))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceFound);

            if (!_eventAggregator.GetEvent<GPSSignalAcquiredEvent>().Contains(HandleGPSSignalAcquired))
                _eventAggregator.GetEvent<GPSSignalAcquiredEvent>().Subscribe(HandleGPSSignalAcquired);

            if (!_eventAggregator.GetEvent<GPSSignalLostEvent>().Contains(HandleGPSSignalLost))
                _eventAggregator.GetEvent<GPSSignalLostEvent>().Subscribe(HandleGPSSignalLost);

            if (!_eventAggregator.GetEvent<DeviceErrorEvent>().Contains(HandleDeviceError))
                _eventAggregator.GetEvent<DeviceErrorEvent>().Subscribe(HandleDeviceError);

            if (!_eventAggregator.GetEvent<ModeChangeEvent>().Contains(HandleModeChange))
                _eventAggregator.GetEvent<ModeChangeEvent>().Subscribe(HandleModeChange);

            if (!_eventAggregator.GetEvent<PinDroppedEvent>().Contains(HandlePinDropped))
                _eventAggregator.GetEvent<PinDroppedEvent>().Subscribe(HandlePinDropped);

            if (!_eventAggregator.GetEvent<UnmappedButtonPressedEvent>().Contains(HandleUnmappedButtonPress))
                _eventAggregator.GetEvent<UnmappedButtonPressedEvent>().Subscribe(HandleUnmappedButtonPress);

            if (!_eventAggregator.GetEvent<DelayedModeEngagedEvent>().Contains(StartDelayedModeCountdown))
                _eventAggregator.GetEvent<DelayedModeEngagedEvent>().Subscribe(StartDelayedModeCountdown);

            if (!_eventAggregator.GetEvent<PatrolStartedEvent>().Contains(HandlePatrolStarted))
                _eventAggregator.GetEvent<PatrolStartedEvent>().Subscribe(HandlePatrolStarted);

            if (!_eventAggregator.GetEvent<VehicleMovingEvent>().Contains(HandleVehicleMoving))
                _eventAggregator.GetEvent<VehicleMovingEvent>().Subscribe(HandleVehicleMoving);

            if (!_eventAggregator.GetEvent<VehicleStationaryEvent>().Contains(HandleVehicleStationary))
                _eventAggregator.GetEvent<VehicleStationaryEvent>().Subscribe(HandleVehicleStationary);
        }

        private void SetDeviceDisconnectedState()
        {
            SetContent(States.NoDevice, BrushNames.Muted, States.DeviceDisconnected, false);
            SetState(States.DeviceDisconnected, FontAwesomeIcon.ChainBroken, BrushNames.EmergencyRed);
            SetGPSState(States.GPSNoSignal, FontAwesomeIcon.Ban, BrushNames.Light);
            SetPatrolState(States.NotPatrolling, FontAwesomeIcon.Circle, BrushNames.EmergencyRed);

            if (_modeService.IsListening)
                _modeService.StopListeningForEvents();

            if (_telemetryService.IsListening)
            {
                _telemetryService.StopSendingTelemetry();
                _telemetryService.SendPowerOff();
            }

            if (!_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(SetDeviceConnectedState))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(SetDeviceConnectedState);

            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceFound))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceFound);

            if (_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(SetDeviceDisconnectedState))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(SetDeviceDisconnectedState);

            if (_eventAggregator.GetEvent<GPSSignalAcquiredEvent>().Contains(HandleGPSSignalAcquired))
                _eventAggregator.GetEvent<GPSSignalAcquiredEvent>().Unsubscribe(HandleGPSSignalAcquired);

            if (_eventAggregator.GetEvent<GPSSignalLostEvent>().Contains(HandleGPSSignalLost))
                _eventAggregator.GetEvent<GPSSignalLostEvent>().Unsubscribe(HandleGPSSignalLost);

            if (_eventAggregator.GetEvent<DeviceErrorEvent>().Contains(HandleDeviceError))
                _eventAggregator.GetEvent<DeviceErrorEvent>().Unsubscribe(HandleDeviceError);

            if (_eventAggregator.GetEvent<ModeChangeEvent>().Contains(HandleModeChange))
                _eventAggregator.GetEvent<ModeChangeEvent>().Unsubscribe(HandleModeChange);

            if (_eventAggregator.GetEvent<PinDroppedEvent>().Contains(HandlePinDropped))
                _eventAggregator.GetEvent<PinDroppedEvent>().Unsubscribe(HandlePinDropped);

            if (_eventAggregator.GetEvent<UnmappedButtonPressedEvent>().Contains(HandleUnmappedButtonPress))
                _eventAggregator.GetEvent<UnmappedButtonPressedEvent>().Unsubscribe(HandleUnmappedButtonPress);

            if (_eventAggregator.GetEvent<DelayedModeEngagedEvent>().Contains(StartDelayedModeCountdown))
                _eventAggregator.GetEvent<DelayedModeEngagedEvent>().Unsubscribe(StartDelayedModeCountdown);

            if (_eventAggregator.GetEvent<PatrolStartedEvent>().Contains(HandlePatrolStarted))
                _eventAggregator.GetEvent<PatrolStartedEvent>().Unsubscribe(HandlePatrolStarted);

            if (_eventAggregator.GetEvent<VehicleMovingEvent>().Contains(HandleVehicleMoving))
                _eventAggregator.GetEvent<VehicleMovingEvent>().Unsubscribe(HandleVehicleMoving);

            if (_eventAggregator.GetEvent<VehicleStationaryEvent>().Contains(HandleVehicleStationary))
                _eventAggregator.GetEvent<VehicleStationaryEvent>().Unsubscribe(HandleVehicleStationary);
        }

        private void SetGPSState(string stateMessage, FontAwesomeIcon icon, string color)
        {
            GPSStateMessage = stateMessage;
            GPSStateIcon = icon.ToString();
            GPSStateIconForeground = (SolidColorBrush)App.Current.FindResource(color);
        }

        private void SetInitialState()
        {
            SetContent(States.Initializing, BrushNames.Muted, States.Authenticating);
            SetState(States.Initializing, FontAwesomeIcon.CircleThin, BrushNames.Light);
            SetPatrolState(States.NotPatrolling, FontAwesomeIcon.CircleThin, BrushNames.EmergencyRed);
            SetGPSState(States.GPSInitializing, FontAwesomeIcon.CircleThin, BrushNames.Light);
        }

        private void SetPatrolState(string stateMessage, FontAwesomeIcon icon, string color)
        {
            PatrolStateMessage = stateMessage;
            PatrolStateIcon = icon.ToString();
            PatrolStateIconForeground = (SolidColorBrush)App.Current.FindResource(color);
        }

        private void SetReadyState()
        {
            if (_deviceService.IsDeviceConnected)
                SetDeviceConnectedState();
            else
                SetDeviceDisconnectedState();
        }

        private void SetState(string stateMessage, FontAwesomeIcon icon, string color)
        {
            StateMessage = stateMessage;
            StateIcon = icon.ToString();
            StateIconForeground = (SolidColorBrush)App.Current.FindResource(color);
        }

        private void SetTaskbarIcon()
        {
            trayIcon.Icon = Icons.pursuitalert_small;
            trayIcon.Visible = true;
            trayIcon.Click += ShowWindow;

            var menuItems = new List<MenuItem>
            {
                new MenuItem(Properties.Resources.UpdateVehicleInfoCommand, new EventHandler((s, e) => OpenSettings())),
                new MenuItem(Properties.Resources.ExitCommand, new EventHandler(Shutdown))
            };
            trayIcon.ContextMenu = new ContextMenu(menuItems.ToArray());
        }

        private void ShowWindow(object sender, EventArgs e)
        {
            System.Windows.Application.Current.MainWindow.Show();
            System.Windows.Application.Current.MainWindow.WindowState = WindowState.Normal;
        }

        private void Shutdown(object sender, EventArgs e)
        {
            Log.Debug("Exit requested from system tray.");

            // TODO: Dispatch any last events that need to be dispatched
            _deviceService.CloseConnection();

            trayIcon.Visible = false;
            trayIcon.Icon.Dispose();
            trayIcon.Dispose();
            App.Current.Shutdown();
        }

        private void StartDelayedModeCountdown(Mode mode)
        {
            if (!_eventAggregator.GetEvent<DelayedModeCanceledEvent>().Contains(CancelDelayedModeCountdown))
                _eventAggregator.GetEvent<DelayedModeCanceledEvent>().Subscribe(CancelDelayedModeCountdown);

            if (!_eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Contains(CountdownTimerTick))
                _eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Subscribe(CountdownTimerTick);

            SetCountdownTimer(string.Format(Properties.Resources.CountdownTimerMessage, mode.Message), mode.ColorName, mode.IncidentDelay);
        }

        #endregion Methods
    }
}