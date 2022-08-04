using FontAwesome.WPF;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using PursuitAlert.Client.Old.Properties;
using PursuitAlert.Domain.Device.Events;
using PursuitAlert.Domain.Device.Payloads.Events;
using PursuitAlert.Domain.Device.Payloads.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PursuitAlert.Client.Old.Dialogs.SerialNumber
{
    public class SerialNumberDialogViewModel : BindableBase, IDialogAware
    {
        #region Fields

        private const string MoveOverYellowBrush = "MoveOverYellowBrush";

        private string _errorMessage;

        private IEventAggregator _eventAggregator;

        private string _icon;

        private SolidColorBrush _iconForeground;

        private string _message;

        private string _serialNumberInput;

        private bool _showError;

        private SubscriptionToken SerialNumberSetEventSubscriptionToken;

        private SubscriptionToken SerialNumberSetFailedEventSubscriptionToken;

        #endregion Fields

        #region Events

        public event Action<IDialogResult> RequestClose;

        #endregion Events

        #region Properties

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string Icon
        {
            get { return _icon; }
            set { _icon = value; }
        }

        public SolidColorBrush IconForeground
        {
            get { return _iconForeground; }
            set { _iconForeground = value; }
        }

        public string Message
        {
            get { return _message; }
            set { _message = value; }
        }

        public string SerialNumberInput
        {
            get => _serialNumberInput;
            set => SetProperty(ref _serialNumberInput, value);
        }

        public DelegateCommand SetSerialNumberCommand { get; set; }

        public bool ShowError
        {
            get => _showError;
            set => SetProperty(ref _showError, value);
        }

        public string Title { get; set; }

        #endregion Properties

        #region Constructors

        public SerialNumberDialogViewModel(IEventAggregator eventAggregator)
        {
            Title = "Press any button";
            Icon = FontAwesomeIcon.Hashtag.ToString();
            IconForeground = (SolidColorBrush)System.Windows.Application.Current.FindResource(MoveOverYellowBrush);
            Message = UserMessages.RequestButtonPress;
            SetSerialNumberCommand = new DelegateCommand(ValidateAndSetSerialNumber);

            _eventAggregator = eventAggregator;

            SerialNumberSetEventSubscriptionToken = eventAggregator.GetEvent<DeviceSerialNumberSetEvent>().Subscribe(HandleSerialNumberSetEvent, true);
            SerialNumberSetFailedEventSubscriptionToken = eventAggregator.GetEvent<SetDeviceSerialNumberFailedEvent>().Subscribe(HandleSerialNumberSetFailedEvent, true);
        }

        #endregion Constructors

        #region Methods

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
        }

        private void HandleSerialNumberSetEvent(string serialNumber)
        {
            _eventAggregator.GetEvent<DeviceSerialNumberSetEvent>().Unsubscribe(SerialNumberSetEventSubscriptionToken);
            _eventAggregator.GetEvent<DeviceSerialNumberCapturedEvent>().Publish(serialNumber);
            App.Current.Dispatcher.Invoke(() => RequestClose?.Invoke(null));
        }

        private void HandleSerialNumberSetFailedEvent(Exception exception)
        {
            ErrorMessage = exception.Message + " More information is in the log file.";
            ShowError = true;
        }

        private void ValidateAndSetSerialNumber()
        {
            ErrorMessage = string.Empty;
            ShowError = false;

            if (string.IsNullOrEmpty(SerialNumberInput))
            {
                ErrorMessage = "Please enter a serial number above";
                ShowError = true;
            }
            // Validate the serial number is 8 characters
            else if (SerialNumberInput.Length != 8)
            {
                ErrorMessage = "Serial number must be 8-characters";
                ShowError = true;
            }
            else
            {
                _eventAggregator.GetEvent<SetDeviceSerialNumberEvent>().Publish(SerialNumberInput);
            }
        }

        #endregion Methods
    }
}