using PursuitAlert.Client.Views;
using Prism.Ioc;
using System.Windows;
using System;
using Serilog;
using System.Reflection;
using Prism.Events;
using PursuitAlert.Client.Events;
using PursuitAlert.Client.Properties;
using System.IO;
using System.Xml.Linq;
using Prism.DryIoc;
using DryIoc;
using PursuitAlert.Client.Services.Security;
using System.Threading;
using PursuitAlert.Client.Services.Device;
using PursuitAlert.Client.Services.Device.Connection;
using PursuitAlert.Client.Services.Device.LED;
using PursuitAlert.Client.Services.Device.Payloads;
using PursuitAlert.Client.Infrastructure.Email;
using PursuitAlert.Client.Dialogs.SettingsDialog;
using PursuitAlert.Client.Infrastructure.API;
using PursuitAlert.Client.Infrastructure.IoTManagement;
using PursuitAlert.Client.Services.GPS;
using PursuitAlert.Client.Infrastructure.SSMService;
using PursuitAlert.Client.Infrastructure.Tokens;
using PursuitAlert.Client.Services.Modes;
using PursuitAlert.Client.Services.Sounds;
using PursuitAlert.Client.Infrastructure.IoTData;
using PursuitAlert.Client.Services.Telemetry;
using System.Security.Principal;
using System.Management;

namespace PursuitAlert.Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        #region Fields

        private bool _hasHandle;

        private const string appName = @"Global\PursuitAlert";

        private static Mutex _mutex = new Mutex(true, appName, out bool _);

        #endregion Fields

        #region Methods

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void Initialize()
        {
            // Set the shutdown mode to ensure the app shuts down gracefully
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Handle any unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;

            // Ensure only one instance of the app runs at a time
            SetMutex();

            // Configure logging
            ConfigureLogging();

            // Upgrade settings if necessary (migrate the settings from a previous version) (https://stackoverflow.com/a/534335)

            // ! This isn't working yet. It's killing all the settings when a new version is built

            //if (Settings.Default.SettingsUpgradeRequired)
            //{
            //    Settings.Default.Upgrade();
            //    Settings.Default.SettingsUpgradeRequired = false;
            //    Settings.Default.Save();
            //}

            WriteLogHeader();

            base.Initialize();
        }

        private void WriteLogHeader()
        {
            Log.Information("\r\n\r\nApplication startup - version {version}", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Log.Debug("------ Device Information ------");
            Log.Debug("Last device serial number: {serialNumber}", Settings.Default.DeviceSerialNumber);
            Log.Debug("------ PC Information ------");
            Log.Debug("PC: {username}", Environment.MachineName);
            Log.Debug("Username: {username}", WindowsIdentity.GetCurrent().Name.ToLower());
            Log.Debug("------ Organization Information ------");
            Log.Debug("Organization Id: {organizationId}", OrganizationSettings.Default.Id);
            Log.Debug("Organization Code: {organizationCode}", OrganizationSettings.Default.Code);
            Log.Debug("Organization Name: {organizationName}", OrganizationSettings.Default.DisplayName);
            Log.Debug("------ Vehicle Information ------");
            Log.Debug("Asset (vehicle) ID: {unitId}", VehicleSettings.Default.Id);
            Log.Debug("Unit ID: {unitId}", VehicleSettings.Default.UnitId);
            Log.Debug("Officer: {officer}", VehicleSettings.Default.Officer);
            if (!string.IsNullOrWhiteSpace(VehicleSettings.Default.SecondaryOfficer))
                Log.Debug("Secondary officer: {secondaryOfficer}", VehicleSettings.Default.SecondaryOfficer);
            if (!string.IsNullOrWhiteSpace(VehicleSettings.Default.Notes))
                Log.Debug("Notes: {notes}", VehicleSettings.Default.Notes);
            Log.Debug("------ Settings ------");
            Log.Debug("Debug mode: {debugMode}", Settings.Default.DebugMode);
            Log.Debug("First run: {firstRun}", Settings.Default.IsFirstRun);
            Log.Debug("Log directory: {logDirectory}", string.IsNullOrWhiteSpace(Settings.Default.LogDirectoryPath) ? string.Empty : Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath));
            Log.Debug("Log recipient: {recipient}", Settings.Default.LogRecipient);
            Log.Debug("Portal address: {address}", Settings.Default.PortalAddress);
            Log.Debug("Stage: {stage}", Settings.Default.Stage);
            Log.Debug("------ End -----\r\n");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var eventAggregator = Container.Resolve<IEventAggregator>();
            eventAggregator.GetEvent<ApplicationExitEvent>().Publish();

            // Release the application mutex
            if (_mutex != null)
            {
                // Release the mutex if we own it
                if (_hasHandle)
                {
                    Log.Verbose("Releasing Pursuit Alert mutex");
                    _mutex.ReleaseMutex();
                    Log.Debug("Mutex released");
                }

                // Close and dispose the mutex
                _mutex.Close();
                _mutex.Dispose();
                Log.Verbose("Mutex closed and disposed");
            }

            Log.Information("Application shutting down (Exit code: {exitCode})", e.ApplicationExitCode);

            base.OnExit(e);
        }

        private string GetProgramFilesx86Path()
        {
            // Workaround for ProgramFiles (x86) environment variable being empty on device
            // manufacturer's test machines: https://stackoverflow.com/a/194223
            if (8 == IntPtr.Size || (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
                return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            return Environment.GetEnvironmentVariable("ProgramFiles");
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            // Read the data in from the registration.xml file left be the installer if this is
            // first run

            var programFilesPath = GetProgramFilesx86Path();
            var registrationPath = Path.Combine(programFilesPath, "Pursuit Alert", "registration.xml");
            if (Settings.Default.IsFirstRun && File.Exists(registrationPath))
            {
                var registrationDocument = XDocument.Load(registrationPath);
                var registrationElement = XElement.Parse(registrationDocument.ToString());

                var organizationCode = (string)registrationElement.Element("ORGCODE");
                var vehicleName = (string)registrationElement.Element("VEHICLENAME");
                var officerName = (string)registrationElement.Element("OFFICERNAME");

                Settings.Default.IsFirstRun = false;
                OrganizationSettings.Default.Code = organizationCode;
                VehicleSettings.Default.UnitId = vehicleName;
                VehicleSettings.Default.Officer = officerName;
                Settings.Default.Save();
            }
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<IEncryptionService, EncryptionService>();

            // Register utilities
            containerRegistry.Register<IEncryptionService, EncryptionService>();

            // Register infrastructure services
            containerRegistry.GetContainer().RegisterDelegate<ISSMService>(services =>
            {
                var encryptionService = services.Resolve<IEncryptionService>();
                return new SSMService(encryptionService, Secrets.Default.AWSKeyId, Secrets.Default.AWSSecretKey);
            });

            containerRegistry.GetContainer().RegisterDelegate<IIoTManagementService>(services =>
            {
                var encryptionService = services.Resolve<IEncryptionService>();
                return new IoTManagementService(encryptionService, Secrets.Default.AWSKeyId, Secrets.Default.AWSSecretKey);
            });

            containerRegistry.GetContainer().RegisterDelegate<IIoTDataService>(services =>
            {
                var encryptionService = services.Resolve<IEncryptionService>();
                var ssmService = services.Resolve<ISSMService>();
                return new IoTDataService(encryptionService, ssmService, Secrets.Default.AWSKeyId, Secrets.Default.AWSSecretKey);
            });

            containerRegistry.GetContainer().RegisterDelegate<ITokenService>(services =>
            {
                var encryptionService = services.Resolve<IEncryptionService>();
                var ssmService = services.Resolve<ISSMService>();
                return new TokenService(ssmService, encryptionService, Secrets.Default.AWSKeyId, Secrets.Default.AWSSecretKey);
            });

            containerRegistry
                .RegisterSingleton<IAPIService, APIService>()
                .RegisterSingleton<ITelemetryService, TelemetryService>()
                .Register<IEmailService, EmailService>();

            // Register dialogs
            containerRegistry
                .RegisterDialog<SettingsDialog, SettingsDialogViewModel>();

            // Register device services
            containerRegistry
                .RegisterSingleton<IDeviceConnectionMonitorService, DeviceConnectionMonitorService>()
                .RegisterSingleton<ILEDService, LEDService>()
                .RegisterSingleton<IPayloadService, PayloadService>()
                .RegisterSingleton<IDeviceService, DeviceService>()
                .Register<ICalculationService, CalculationService>()
                .RegisterSingleton<IModeService, ModeService>()
                .Register<ISoundPlayerService, SoundPlayerService>();
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

                // Limit the size of log files so that larger files get split (files over 20MB
                // cannot be sent via email)
                .WriteTo.File(Path.Combine(Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath), "log.log"), rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 20000000)
                .CreateLogger();
        }

        private void LogUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal((Exception)e.ExceptionObject, "Fatal error encountered: {0}", ((Exception)e.ExceptionObject).Message);

            // TODO: Send logs to AWS IoT
        }

        private void SetMutex()
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

        #endregion Methods
    }
}