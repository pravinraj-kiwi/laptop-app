using Newtonsoft.Json;
using Polly;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using PursuitAlert.Client.Old.Dialogs.NewDeviceConnected;
using PursuitAlert.Client.Old.Dialogs.SerialNumber;
using PursuitAlert.Client.Old.Dialogs.ServerConnectionFailed;
using PursuitAlert.Client.Old.Dialogs.VehicleSettings;
using PursuitAlert.Client.Old.Properties;
using PursuitAlert.Domain.API.Services;
using PursuitAlert.Domain.Configuration.Models;
using PursuitAlert.Domain.Configuration.Services;
using PursuitAlert.Domain.Device.Events;
using PursuitAlert.Domain.Device.Models;
using PursuitAlert.Domain.Device.Services;
using PursuitAlert.Domain.Modes.Events;
using PursuitAlert.Domain.Modes.Services;
using PursuitAlert.Domain.Publishing.Events;
using PursuitAlert.Domain.Publishing.Services;
using PursuitAlert.Domain.Security.Services;
using Serilog;
using System;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PursuitAlert.Client.Old.Modes
{
    public class InitializingViewModel : BindableBase, IRegionMemberLifetime
    {
        #region Properties

        public bool Animate
        {
            get { return _animate; }
            set { SetProperty(ref _animate, value); }
        }

        public bool HasDisconnected { get; private set; }

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

        public bool SerialNumberDialogOpen { get; private set; }

        public string Subheading
        {
            get { return _subheading; }
            set { SetProperty(ref _subheading, value); }
        }

        #endregion Properties

        #region Fields

        private const string Connected = "Connected";

        private const string EmergencyRedBrush = "EmergencyRedBrush";

        private const string Initializing = "Initializing";

        private const string LightBrush = "LightBrush";

        private const string Offline = "Offline";

        private readonly IAPIService _apiService;

        private readonly string _awsAccessKeyId;

        private readonly string _awsSecretKey;

        private readonly string _awsServiceAccountClientId;

        private readonly string _awsServiceAccountClientSecret;

        private readonly IConfigurationMonitorService _configurationService;

        private readonly IDeviceMonitorService _deviceServiceMonitor;

        private readonly IDialogService _dialogService;

        private readonly IEncryptionService _encryptionService;

        private readonly IEventAggregator _eventAggregator;

        private readonly IModeService _modeService;

        private readonly IRegionManager _navigationService;

        private readonly IPublishingService _publishingService;

        private bool _animate;

        private bool _connectionFailureDialogShown;

        private string _heading;

        private SolidColorBrush _logoColor;

        private string _subheading;

        private bool mqttConnectionTaskRunning = false;

        private bool mqttReconnectionBuffer;

        #endregion Fields

        #region Constructors

        public InitializingViewModel(
            IDeviceMonitorService deviceMonitorService,
            IPublishingService publishingService,
            IEncryptionService encryptionService,
            IRegionManager navigationService,
            IDialogService dialogService,
            IModeService modeService,
            IConfigurationMonitorService configurationService,
            IAPIService apiService,
            IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;
            _publishingService = publishingService;
            _encryptionService = encryptionService;
            _navigationService = navigationService;
            _modeService = modeService;
            _deviceServiceMonitor = deviceMonitorService;
            _apiService = apiService;
            _configurationService = configurationService;

            Animate = false;
            Heading = Initializing;
            LogoColor = (SolidColorBrush)System.Windows.Application.Current.FindResource(LightBrush);
            Subheading = "Searching for device";

            var modePolicy = Policy
                .Handle<InvalidOperationException>()
                .Retry(4, (ex, attemptNumber) =>
                {
                    Log.Warning(ex, "Failed to initialize modes. Retrying ({attempt}).", attemptNumber);
                });

            modePolicy.Execute(() => InitializeModeService());

            _awsAccessKeyId = _encryptionService.Decrypt(Settings.Default.AccessKeyId);
            _awsSecretKey = _encryptionService.Decrypt(Settings.Default.SecretAccessKey);
            Task.Run(async () =>
            {
                await _apiService.SetCredentials(_awsAccessKeyId, _awsSecretKey).ConfigureAwait(false);

                StartEventSubscriptions();
                StartDeviceListener();
            });
        }

        #endregion Constructors

        #region Methods

        private void BeginPatrol() => _eventAggregator.GetEvent<PatrolStartedEvent>().Publish();

        private void BeginServerAuthentication(string serialNumber)
        {
            DeviceSettings.Default.DeviceSerialNumber = serialNumber;
            DeviceSettings.Default.Save();
            Log.Verbose("Serial number saved");

            Task.Run(async () =>
            {
                // The publishing service makes sure the device is registered as a thing in AWS
                await _publishingService.SetCredentials(_awsAccessKeyId, _awsSecretKey, Settings.Default.IoTBrokerEndpoint, DeviceSettings.Default.DeviceSerialNumber, Settings.Default.Stage).ConfigureAwait(false);
                await EnsureServerConfiguration();
            });
        }

        private async Task EnsureServerConfiguration()
        {
            // Make sure the organization code in settings is valid
            var organization = await _apiService.GetOrganizationByOrganizationCode(Settings.Default.OrganizationCode);
            if (organization == null)
            {
                // The organization doesn't exist, open the settings panel to let the user try again
                App.Current.Dispatcher.Invoke(() =>
                {
                    _dialogService.Show(nameof(SettingsDialog), _ =>
                    {
                        _publishingService.SetCredentials(_awsAccessKeyId, _awsSecretKey, Settings.Default.IoTBrokerEndpoint, DeviceSettings.Default.DeviceSerialNumber, Settings.Default.Stage).Wait();
                    });
                });
            }
            else
            {
                // Make sure the device exists; if it doesn't, create ie
                var device = await _apiService.GetDeviceNodeBySerialNumber(DeviceSettings.Default.DeviceSerialNumber);
                if (device == null)
                    await _apiService.CreateDeviceNode(DeviceSettings.Default.DeviceSerialNumber, organization.Id);

                // Make sure the vehicle exists; if it doesn't, create it
                var vehicle = await _apiService.GetVehicleByUnitId(Settings.Default.UnitId);
                if (vehicle == null)
                    vehicle = await _apiService.CreateVehicle(Settings.Default.UnitId, DeviceSettings.Default.DeviceSerialNumber, Settings.Default.OrganizationCode, Settings.Default.Officer, Settings.Default.SecondaryOfficer, Settings.Default.Notes);

                await _apiService.EnsureAssetDeviceAssociation(Settings.Default.UnitId, vehicle.Id, DeviceSettings.Default.DeviceSerialNumber);
            }
        }

        private void HandleAuthenticationFailure(Exception error)
        {
            // Set a delay so that the app isn't constantly trying to connect
            mqttReconnectionBuffer = true;
            Task.Run(() =>
            {
                Task.Delay(30000).Wait();
                mqttReconnectionBuffer = false;
            });

            SetHeadings(Offline, "Couldn't connect to server", EmergencyRedBrush);
            if (!_connectionFailureDialogShown)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    _dialogService.ShowDialog(nameof(ServerConnectionFailedDialog), null, result => { _connectionFailureDialogShown = true; });
                });
            }
        }

        private void HandleBrokerConnected(string server)
        {
            SetHeadings(Connected, "Connected to server", LightBrush);
            InitializeConfigurationMonitorService();
            BeginPatrol();

            if (_eventAggregator.GetEvent<BrokerConnectedEvent>().Contains(HandleBrokerConnected))
                _eventAggregator.GetEvent<BrokerConnectedEvent>().Unsubscribe(HandleBrokerConnected);

            if (!_eventAggregator.GetEvent<BrokerDisconnectedEvent>().Contains(HandleBrokerDisconnected))
                _eventAggregator.GetEvent<BrokerDisconnectedEvent>().Subscribe(HandleBrokerDisconnected);

            App.Current.Dispatcher.Invoke(() =>
            {
                _navigationService.RequestNavigate(ContentRegionNames.ModeRegion, nameof(ReadyView));
            });
        }

        private void HandleBrokerDisconnected(string brokerUrl)
        {
            if (!_eventAggregator.GetEvent<BrokerConnectedEvent>().Contains(HandleBrokerConnected))
                _eventAggregator.GetEvent<BrokerConnectedEvent>().Subscribe(HandleBrokerConnected);
        }

        private void HandleDeviceConnected(SerialPort port)
        {
            if (!_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleDeviceDisconnected))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleDeviceDisconnected);

            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceConnected))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceConnected);

            if (!_eventAggregator.GetEvent<DeviceSerialNumberCapturedEvent>().Contains(HandleSerialNumberCaptured))
                _eventAggregator.GetEvent<DeviceSerialNumberCapturedEvent>().Subscribe(HandleSerialNumberCaptured);
        }

        private void HandleDeviceDisconnected()
        {
            SetHeadings(Offline, "Device disconnected", LightBrush);
            HasDisconnected = true;

            if (_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleDeviceDisconnected))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(HandleDeviceDisconnected);

            if (!_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceConnected))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceConnected);
        }

        private void HandleDeviceError(Exception error)
        {
            SetHeadings(Offline, "Couldn't connect to device", EmergencyRedBrush);
            HasDisconnected = true;
        }

        private void HandleSerialNumberCaptured(string serialNumber)
        {
            Log.Debug("Serial number captured: {0}", serialNumber);
            if (string.IsNullOrWhiteSpace(DeviceSettings.Default.DeviceSerialNumber))
                BeginServerAuthentication(serialNumber);
            else if (!DeviceSettings.Default.DeviceSerialNumber.Equals(serialNumber))
            {
                // A new device has been connected, the authentication process needs to happen again
                Log.Information("The serial number in settings {0} does not match the captured serial number {1}", DeviceSettings.Default.DeviceSerialNumber, serialNumber);
                DeviceSettings.Default.DeviceSerialNumber = serialNumber;
                BeginServerAuthentication(serialNumber);
            }
            else if (!_publishingService.IsConnected && !_publishingService.IsConnecting)
                BeginServerAuthentication(serialNumber);
            else if (_navigationService.Regions[ContentRegionNames.ModeRegion].NavigationService.Journal.CurrentEntry == null
                || !_navigationService.Regions[ContentRegionNames.ModeRegion].NavigationService.Journal.CurrentEntry.Uri.Equals(nameof(ReadyView)))
            {
                // If the device has been disconnected, but is not reconnected, we're not in the
                // process of reconnecting, and current view is not the ready view, navigate to it
                App.Current.Dispatcher.Invoke(() => _navigationService.RequestNavigate(ContentRegionNames.ModeRegion, nameof(ReadyView)));
            }
            if (_eventAggregator.GetEvent<DeviceSerialNumberCapturedEvent>().Contains(HandleSerialNumberCaptured))
                _eventAggregator.GetEvent<DeviceSerialNumberCapturedEvent>().Unsubscribe(HandleSerialNumberCaptured);
        }

        private void InitializeConfigurationMonitorService()
        {
            try
            {
                Log.Verbose("Starting configuration monitor");

                var configurationUrl =
                    $"{Settings.Default.APIBaseUrl.TrimEnd('/')}" +
                    $"/{Settings.Default.ConfigurationURL.TrimEnd('/').TrimStart('/')}" +
                    $"/{DeviceSettings.Default.DeviceSerialNumber}";

                Log.Verbose("Listening for changes in configuration at: {0}", configurationUrl);
                _configurationService.ListenForChanges(configurationUrl,
                    listenerInterval: Settings.Default.ConfigurationIntervalCheckInMS,
                    lastConfigurationRetrievedUTC: Settings.Default.ConfigurationLastUpdated);
                Log.Debug("Configuration monitor service started.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Cannot start configuration monitor service. Configurations will not be updated.");
            }
        }

        private void InitializeModeService()
        {
            Log.Verbose("Loading configuration from {0}", Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath));
            var configJson = File.ReadAllText(Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath));
            var config = JsonConvert.DeserializeObject<JurisdictionConfiguration>(configJson);
            Log.Debug("Loaded button configuration from {0}", Environment.ExpandEnvironmentVariables(Settings.Default.ConfigurationFilePath));

            if (config == null)
                throw new InvalidOperationException("Loaded configuration is null");

            _modeService.SetSwitchMapping(config.Modes);
        }

        private void SetHeadings(string heading, string subheading, string logoColor)
        {
            Heading = heading;
            Subheading = subheading;
            try
            {
                LogoColor = (SolidColorBrush)System.Windows.Application.Current.FindResource(logoColor);
            }
            catch (NullReferenceException)
            {
                Log.Debug("Attempted to set headings in Initializing View model, but app is shutting down. Values: {0}, {1}, {2}", heading, subheading, logoColor);
            }
        }

        private void ShowPressAButtonDialog()
        {
            SetHeadings(Initializing, "Waiting for serial number", LightBrush);
            if (!SerialNumberDialogOpen)
            {
                SerialNumberDialogOpen = true;
                App.Current.Dispatcher.Invoke(() =>
                {
                    _dialogService.Show(nameof(SerialNumberDialog), null, result => SerialNumberDialogOpen = false);
                });
            }
        }

        private void StartDeviceListener()
        {
            var deviceConnectionParameters = new DeviceConnectionParameters
            {
                SwitchActivationJitterInMS = DeviceSettings.Default.SwitchActivationJitterInMS,
                BaudRate = DeviceSettings.Default.BaudRate,
                ButtonCount = DeviceSettings.Default.ButtonCount,
                DataBits = DeviceSettings.Default.DataBits,
                Handshake = DeviceSettings.Default.Handshake,
                Parity = DeviceSettings.Default.Parity,
                PID = DeviceSettings.Default.PID,
                ReadTimeout = DeviceSettings.Default.ReadTimeout,
                StopBits = DeviceSettings.Default.StopBits,
                VID = DeviceSettings.Default.VID,
                WriteTimeout = DeviceSettings.Default.WriteTimeout
            };
            _deviceServiceMonitor.StartDeviceListener(deviceConnectionParameters);
        }

        private void StartEventSubscriptions()
        {
            // Device initialization events
            _eventAggregator.GetEvent<DeviceInitializingEvent>().Subscribe(() => SetHeadings(Initializing, "Initializing device", LightBrush));
            _eventAggregator.GetEvent<DeviceInitializedEvent>().Subscribe(() => SetHeadings(Initializing, "Device initialized", LightBrush));
            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleDeviceDisconnected);
            _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceConnected);
            _eventAggregator.GetEvent<NoDeviceFoundEvent>().Subscribe(() => SetHeadings(Offline, "No device found", EmergencyRedBrush));

            //_eventAggregator.GetEvent<DeviceFoundEvent>().Subscribe(portName => SetHeadings(Initializing, "Device found", LightBrush));
            _eventAggregator.GetEvent<DeviceErrorEvent>().Subscribe(HandleDeviceError);
            _eventAggregator.GetEvent<DeviceSerialNumberNotFoundEvent>().Subscribe(() => ShowPressAButtonDialog());
            _eventAggregator.GetEvent<DeviceSerialNumberCapturedEvent>().Subscribe(HandleSerialNumberCaptured);

            // Mqtt initialization events
            _eventAggregator.GetEvent<BrokerConnectedEvent>().Subscribe(HandleBrokerConnected);
        }

        #endregion Methods
    }
}