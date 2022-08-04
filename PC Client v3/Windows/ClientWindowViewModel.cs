using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using PursuitAlert.Client.Old.Properties;
using PursuitAlert.Domain.Application.Events;
using PursuitAlert.Domain.Device.Payloads.Events;
using PursuitAlert.Domain.Modes.Events;
using PursuitAlert.Domain.Modes.Models;
using PursuitAlert.Domain.Publishing.Events;
using PursuitAlert.Domain.Application.Utilities;
using Serilog;
using System.Text.RegularExpressions;
using Prism.Services.Dialogs;
using PursuitAlert.Client.Old.Dialogs.VehicleSettings;

namespace PursuitAlert.Client.Old.Windows
{
    public class ClientWindowViewModel : BindableBase
    {
        #region Properties

        public SolidColorBrush CountdownBackgroundColor
        {
            get { return _countdownBackgroundColor; }
            set { SetProperty(ref _countdownBackgroundColor, value); }
        }

        public string CountdownCancelation
        {
            get => _countdownCancelation;
            set => SetProperty(ref _countdownCancelation, value);
        }

        public string CountdownMessage
        {
            get => _countdownMessage;
            set => SetProperty(ref _countdownMessage, value);
        }

        public DelegateCommand Hide { get; private set; }

        public bool IsDebugMode
        {
            get { return Settings.Default.DebugMode; }
        }

        public bool IsDemoMode
        {
            get { return Settings.Default.DemoMode; }
        }

        public string NotificationMessage
        {
            get => _notificationMessage;

            set
            {
                SetProperty(ref _notificationMessage, value);
                ((Storyboard)((ClientWindow)System.Windows.Application.Current.MainWindow).Resources[ShowNotificationAnimationName]).Begin();
            }
        }

        public DelegateCommand OpenHistory { get; private set; }

        public DelegateCommand OpenSettings { get; private set; }

        public DelegateCommand SendLogsInEmail { get; private set; }

        public bool ShowCountdownTimer
        {
            get => _showCountdownTimer;
            set => SetProperty(ref _showCountdownTimer, value);
        }

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public string Version
        {
            get => $"v{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}";
        }

        #endregion Properties

        #region Fields

        private const string ShowNotificationAnimationName = "ShowNotification";

        private readonly IDialogService _dialogService;

        private readonly IEventAggregator _eventAggregator;

        private readonly IRegionManager _regionManager;

        private readonly NotifyIcon notificationAreaIcon = new NotifyIcon();

        private SolidColorBrush _countdownBackgroundColor;

        private string _countdownCancelation;

        private string _countdownMessage;

        private string _notificationMessage;

        private bool _showCountdownTimer;

        private string _title = "Pursuit Alert";

        #endregion Fields

        #region Constructors

        public ClientWindowViewModel(IRegionManager regionManager, IEventAggregator eventAggregator, IDialogService dialogService)
        {
            _regionManager = regionManager;
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;

            _eventAggregator.GetEvent<DelayedModeRequestedEvent>().Subscribe(HandleCountdown);
            _eventAggregator.GetEvent<DelayedModeCancelRequestedEvent>().Subscribe(HandleCancelCountdownRequest);
            _eventAggregator.GetEvent<DelayedModeCanceledEvent>().Subscribe(HandleCanceledCountdown);
            _eventAggregator.GetEvent<ModeChangeEvent>().Subscribe(HandleModeChange);
            _eventAggregator.GetEvent<DeviceSwitchActivatedEvent>().Subscribe(HandleSwitchChange);

            OpenHistory = new DelegateCommand(_openHistory);
            SendLogsInEmail = new DelegateCommand(_sendLogsInEmail);
            Hide = new DelegateCommand(_hide);
            OpenSettings = new DelegateCommand(_openSettings);

            _buildContextMenu();
        }

        #endregion Constructors

        #region Methods

        private void _buildContextMenu()
        {
            notificationAreaIcon.Icon = PursuitAlertIcons.pursuitalert_small;
            notificationAreaIcon.Visible = true;
            notificationAreaIcon.Click += (s, e) => _show();
            var menuItems = new List<MenuItem>{
                new MenuItem("Update vehicle info...", new EventHandler((s, e) => {
                    App.Current.Dispatcher.Invoke(() => _dialogService.ShowDialog(nameof(SettingsDialog), new DialogParameters("tab=vehicle_info"), null));
                })),
                new MenuItem("Exit", new EventHandler((s, e) =>
                {
                    notificationAreaIcon.Dispose();
                    System.Windows.Application.Current.Shutdown();
                }))
            };

            notificationAreaIcon.ContextMenu = new ContextMenu(menuItems.ToArray());
            _eventAggregator.GetEvent<ApplicationExitEvent>().Subscribe(HandleExit);
        }

        private string _getEmailPath()
        {
            var directory = new DirectoryInfo(Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath));
            return directory.GetFiles("*.eml")
                .OrderByDescending(f => f.LastWriteTime)
                .First()
                .FullName;
        }

        private IEnumerable<string> _getLogFilePathsFromToday()
        {
            var directory = new DirectoryInfo(Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath));

            return directory.GetFiles("*.log")
                .Where(f => f.LastWriteTime.Date == DateTime.Now.Date)
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => f.FullName);

            //return directory.GetFiles("*.log")
            //    .OrderByDescending(f => f.LastWriteTime)
            //    .First()
            //    .FullName;
        }

        private void _hide()
        {
            System.Windows.Application.Current.MainWindow.Hide();
            System.Windows.Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        private void _openHistory() => Process.Start(Settings.Default.PortalURL);

        private void _openSettings() => App.Current.Dispatcher.Invoke(() => _dialogService.ShowDialog(nameof(SettingsDialog), null, null));

        private void _sendLogsInEmail()
        {
            // https://stackoverflow.com/a/25586282
            var message = new MailMessage
            {
                From = new MailAddress("logs@pursuitalert.com"),
                Subject = "Pursuit Alert Desktop App Logs",
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(Settings.Default.LogRecipient));

            var logPathsFromToday = _getLogFilePathsFromToday();

            // We have to read the file in manually to a new stream. If we don't read it into a new
            // stream, the new Attachment constructor will throw an error that the file is in use,
            // so we have to read the file manually in a less invasive way and the pass the stream
            // to the new Attachment constructor.

            foreach (var attachmentFilePath in logPathsFromToday)
            {
                // Each memory stream is disposed in the message.Save extension method
                var memoryStream = new MemoryStream();

                // https://stackoverflow.com/a/64462894
                using (var fileStream = new FileStream(attachmentFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                var fileName = Path.GetFileName(attachmentFilePath);
                message.Attachments.Add(new Attachment(memoryStream, fileName));
            }

            var bodyContent = new StringBuilder();
            bodyContent.Append("<p>If you need to provide additional information, add it here.</p>");
            bodyContent.Append($"<p>{new string('-', 48)} Do not write below this line {new string('-', 48)}</p>");
            bodyContent.Append("<br>");
            bodyContent.Append($"<p><strong>Device Serial Number:</strong> {DeviceSettings.Default.DeviceSerialNumber}</p>");
            bodyContent.Append($"<p><strong>Application Version:</strong> {Version}</p>");
            message.Body = bodyContent.ToString();

            message.Save(Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath));

            var emailPath = _getEmailPath();

            // Remove the from and sender information from the eml file so Outlook opens with the
            // option to send from the default from address
            var emailFileContent = File.ReadAllText(emailPath);
            emailFileContent = Regex.Replace(emailFileContent, @"X-Sender: .+\n", "X-Sender: \n");
            emailFileContent = Regex.Replace(emailFileContent, @"From: .+\n", "From: \n");

            // Add the X-Unsent header so it's opened as a new message https://stackoverflow.com/a/25586282
            emailFileContent = $"X-Unsent: 1\n{emailFileContent}";
            File.WriteAllText(emailPath, emailFileContent);

            Process.Start(emailPath);
        }

        private void _show()
        {
            System.Windows.Application.Current.MainWindow.Show();
            System.Windows.Application.Current.MainWindow.WindowState = WindowState.Normal;
        }

        private void HandleCancelCountdownRequest(Mode mode)
        {
            CountdownMessage = string.Empty;
            CountdownCancelation = string.Empty;
        }

        private void HandleCanceledCountdown(Mode mode)
        {
            ShowCountdownTimer = false;
            CountdownMessage = string.Empty;
            CountdownCancelation = string.Empty;
            CountdownBackgroundColor = null;
        }

        private void HandleCountdown(Mode newMode)
        {
            ShowCountdownTimer = true;
            CountdownMessage = $"{newMode.Name} activating in";
            CountdownCancelation = $"Press any key to cancel";
            CountdownBackgroundColor = (SolidColorBrush)App.Current.FindResource(newMode.ColorName);
        }

        private void HandleExit(int exitCode)
        {
            if (notificationAreaIcon != null)
            {
                notificationAreaIcon.Icon = null;
                notificationAreaIcon.Dispose();
            }

            _eventAggregator.GetEvent<ApplicationExitEvent>().Unsubscribe(HandleExit);
        }

        private void HandleModeChange(ModeChangeEventArgs newMode) => ShowCountdownTimer = false;

        private void HandleSwitchChange(int switchNo)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.MainWindow.Show();

                // If the window is minimized open it back up
                if (System.Windows.Application.Current.MainWindow.WindowState == WindowState.Minimized)
                    System.Windows.Application.Current.MainWindow.WindowState = WindowState.Normal;

                // If the window is not in the foreground (it should be), bring it to the foreground
                if (!System.Windows.Application.Current.MainWindow.IsActive)
                    System.Windows.Application.Current.MainWindow.Activate();
            });
        }

        #endregion Methods
    }
}