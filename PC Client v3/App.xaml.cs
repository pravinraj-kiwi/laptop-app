using Prism.Modularity;
using Prism.Ioc;
using PursuitAlert.Client.Old.Properties;
using Serilog;
using System;
using System.IO;
using System.Windows;
using Prism.Events;
using PursuitAlert.Client.Old.Windows;
using PursuitAlert.Domain.Device.Services;
using PursuitAlert.Domain.Modes.Services;
using PursuitAlert.Application.Device.Services;
using PursuitAlert.Application.Modes.Services;
using PursuitAlert.Domain.Device.Payloads.Services;
using PursuitAlert.Application.Device.Payloads.Services;
using PursuitAlert.Domain.GPS.Services;
using PursuitAlert.Application.GPS.Services;
using PursuitAlert.Client.Old.Modes;
using Prism.Regions;
using PursuitAlert.Client.Old.States;
using System.Text;
using PursuitAlert.Client.Old.Dialogs.SerialNumber;
using PursuitAlert.Domain.Configuration.Services;
using PursuitAlert.Application.Configuration.Services;
using PursuitAlert.Domain.Audio.Services;
using PursuitAlert.Client.Old.Audio.Services;
using System.Threading;
using Microsoft.Win32;
using System.Windows.Threading;
using PursuitAlert.Domain.Device.Models;
using PursuitAlert.Client.Old.Dialogs.NewDeviceConnected;
using PursuitAlert.Client.Old.Dialogs.ServerConnectionFailed;
using PursuitAlert.Domain.Security.Services;
using PursuitAlert.Application.Security.Services;
using PursuitAlert.Domain.Publishing.Services;
using PursuitAlert.Application.Publishing.Services;
using System.Reflection;
using PursuitAlert.Domain.Application.Events;
using PursuitAlert.Client.Old.Dialogs.VehicleSettings;
using PursuitAlert.Domain.API.Services;
using PursuitAlert.Application.API.Services;
using System.Xml.Linq;

namespace PursuitAlert.Client.Old
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        #region Fields

        private const string appName = @"Global\PursuitAlert";

        private static bool _hasHandle = false;

        private static Mutex _mutex = new Mutex(true, appName, out bool _);

        #endregion Fields

        #region Methods

        protected override void Initialize()
        {
            // Handle the device going to sleep and waking up
            SystemEvents.PowerModeChanged += HandleSystemPowerModeChange;

            // Handle unhandled exceptions
            Current.DispatcherUnhandledException += HandleDispatchUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

            // Ensure only one instance of the app runs at a time
            ConfigureMutex();

            // Configure logging
            ConfigureLogging();

            Log.Information("Application initializing (Version: {0})", Assembly.GetExecutingAssembly().GetName().Version.ToString());

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            base.Initialize();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            base.ConfigureModuleCatalog(moduleCatalog);
        }

        protected override Window CreateShell()
        {
            var shell = Container.Resolve<ClientWindow>();
            System.Windows.Application.Current.MainWindow = shell;
            return shell;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var eventAggregator = Container.Resolve<IEventAggregator>();
            eventAggregator.GetEvent<ApplicationExitEvent>().Publish(e.ApplicationExitCode);

            Log.Information("Application shutting down (Exit code: {exitCode})", e.ApplicationExitCode);

            if (_mutex != null)
            {
                // Release the mutex if we own it
                if (_hasHandle)
                    _mutex.ReleaseMutex();

                // Close & dispose it
                _mutex.Close();
                _mutex.Dispose();
            }

            base.OnExit(e);
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            //Set configuration directory path
            var configurationFilePath = Environment.ExpandEnvironmentVariables(Client.Old.Properties.Settings.Default.ConfigurationFilePath);

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
                var buttonConfigurationJson = Encoding.Default.GetString(FileResources.defaultconfig);
                File.WriteAllText(configurationFilePath, buttonConfigurationJson);
                Log.Debug("Default button configuration file created {0}", configurationFilePath);
            }

            // Check to see if the registration.xaml file left by the installer exists
            var registrationPath = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), "Pursuit Alert", "registration.xml");
            if (Settings.Default.FirstRun && File.Exists(registrationPath))
            {
                var registrationDocument = XDocument.Load(registrationPath);
                var registrationElement = XElement.Parse(registrationDocument.ToString());

                var organizationCode = (string)registrationElement.Element("ORGCODE");
                var vehicleName = (string)registrationElement.Element("VEHICLENAME");
                var officerName = (string)registrationElement.Element("OFFICERNAME");

                Settings.Default.FirstRun = false;
                Settings.Default.OrganizationCode = organizationCode;
                Settings.Default.UnitId = vehicleName;
                Settings.Default.Officer = officerName;
                Settings.Default.Save();
            }

            // Ensure required settings are available
            if (string.IsNullOrWhiteSpace(Settings.Default.AccessKeyId))
                throw new ArgumentNullException(nameof(Settings.Default.AccessKeyId));

            if (string.IsNullOrWhiteSpace(Settings.Default.SecretAccessKey))
                throw new ArgumentNullException(nameof(Settings.Default.SecretAccessKey));

            // Ensure required services have been initialized
            _ = Container.Resolve<IDeviceMonitorService>();
            _ = Container.Resolve<IDeviceService>();
            _ = Container.Resolve<IModeService>();
            _ = Container.Resolve<IBackgroundJobService>();

            // Navigate to the default mode view
            var regionManager = Container.Resolve<IRegionManager>();
            regionManager.RequestNavigate(ContentRegionNames.ModeRegion, nameof(InitializingView));
            regionManager.RequestNavigate(ContentRegionNames.StateRegion, nameof(CurrentState));
        }

        protected override void RegisterRequiredTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IEventAggregator, EventAggregator>();
            base.RegisterRequiredTypes(containerRegistry);
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // TODO: Use RegisterDelegate here to handle setup of services during startup https://github.com/dadhi/DryIoc/blob/master/docs/DryIoc.Docs/RegisterResolve.md#registerdelegate
            containerRegistry
                .Register<ISoundPlayerService, SoundPlayerService>()
                .RegisterSingleton<IConfigurationMonitorService, ConfigurationMonitorService>()
                .Register<IDevicePayloadService, DevicePayloadService>()
                .RegisterSingleton<IDeviceMonitorService, DeviceMonitorService>()
                .RegisterSingleton<IDeviceService, DeviceService>()
                .Register<IGPSCalculationService, GPSCalculationService>()
                .RegisterSingleton<IModeService, ModeService>()
                .RegisterSingleton<IBackgroundJobService, BackgroundJobService>()
                .Register<IMessageBuilderService, MessageBuilderService>()
                .Register<IEncryptionService, EncryptionService>()
                .RegisterSingleton<IPublishingService, PublishingService>()
                .RegisterSingleton<IAPIService, APIService>();

            // Register client window views
            containerRegistry.RegisterForNavigation<InitializingView, InitializingViewModel>();
            containerRegistry.RegisterForNavigation<ReadyView, ReadyViewModel>();
            containerRegistry.RegisterForNavigation<CurrentState, CurrentStateViewModel>();

            // Register dialogs
            containerRegistry.RegisterDialog<SerialNumberDialog, SerialNumberDialogViewModel>();
            containerRegistry.RegisterDialog<NewDeviceConnectedDialog, NewDeviceConnectedDialogViewModel>();
            containerRegistry.RegisterDialog<ServerConnectionFailedDialog, ServerConnectionFailedDialogViewModel>();
            containerRegistry.RegisterDialog<SettingsDialog, SettingsDialogViewModel>();
        }

        private void ConfigureLogging()
        {
            // TODO: Configure log level based on configuration value

            if (!Directory.Exists(Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath)))
                Directory.CreateDirectory(Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath));

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
#if DEBUG
                .Enrich.WithProperty("User", Environment.UserName)
                .Enrich.WithProperty("Configuration", "Debug")
                .WriteTo.Debug()
#else
                .Enrich.WithProperty("User", Environment.UserName)
                .Enrich.WithProperty("Configuration", "Release")
#endif
                .WriteTo.File(Path.Combine(Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath), "log.log"), rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        private void ConfigureMutex()
        {
            // Only allow one instance of the app running at a time: https://www.c-sharpcorner.com/UploadFile/f9f215/how-to-restrict-the-application-to-just-one-instance/
            GC.KeepAlive(_mutex);
            try
            {
                _hasHandle = _mutex.WaitOne(0, false);
                if (!_hasHandle)
                {
                    // A new mutex was not created, meaning one with the name PursuitAlert existed
                    // and the application was already running. Do not launch.
                    App.Current.Shutdown();
                    return;
                }
            }
            catch (AbandonedMutexException)
            {
                _hasHandle = true;
            }
        }

        private void HandleDispatchUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "[DispatcherUnhandled] Fatal error encountered: {0}.", e.Exception.Message);

            // Try to send a message to the logging topic
            try
            {
                var mqttService = Container.Resolve<IPublishingService>();
                mqttService.SendLogMessage($"[Fatal:DispatcherUnhandled] {e.Exception}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send final SOS message");
            }
        }

        private void HandleSystemPowerModeChange(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                // System is waking back up from sleeping
                Log.Information("System is waking from sleep mode.");

                var deviceMonitorService = Container.Resolve<IDeviceMonitorService>();
                if (deviceMonitorService != null && !deviceMonitorService.DeviceIsConnected)
                {
                    Log.Debug("Device is not connected. Starting device listener");
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
                    deviceMonitorService.StartDeviceListener(deviceConnectionParameters);
                }
            }
            else
            {
                // System is going to sleep
                Log.Debug("System going to sleep.");
            }
        }

        private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.ExceptionObject as Exception, "[AppDomain] Fatal error encountered: {0}.", (e.ExceptionObject as Exception).Message);

            // Try to send a message to the logging topic
            try
            {
                var mqttService = Container.Resolve<IPublishingService>();
                mqttService.SendLogMessage($"[Fatal:AppDomain] {(e.ExceptionObject as Exception)}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send final SOS message");
            }
        }

        #endregion Methods
    }
}