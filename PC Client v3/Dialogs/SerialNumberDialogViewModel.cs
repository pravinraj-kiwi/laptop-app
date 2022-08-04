using FontAwesome.WPF;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using PursuitAlert.Client.Properties;
using PursuitAlert.Domain.Device.Events;
using PursuitAlert.Domain.Device.Payloads.Events;
using PursuitAlert.Domain.Device.Payloads.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PursuitAlert.Client.Dialogs
{
    public class SerialNumberDialogViewModel : BindableBase, IDialogAware
    {
        #region Fields

        private const string ActiveShooterYellowBrush = "ActiveShooterYellowBrush";

        private IEventAggregator _eventAggregator;

        private string _icon;

        private SolidColorBrush _iconForeground;

        private string _message;

        private SubscriptionToken SwitchStatusReceivedEventSubscriptionToken;

        #endregion Fields

        #region Events

        public event Action<IDialogResult> RequestClose;

        #endregion Events

        #region Properties

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

        public string Title { get; set; }

        #endregion Properties

        #region Constructors

        public SerialNumberDialogViewModel(IEventAggregator eventAggregator)
        {
            Title = "Press any button";
            Icon = FontAwesomeIcon.Hashtag.ToString();
            IconForeground = (SolidColorBrush)System.Windows.Application.Current.FindResource(ActiveShooterYellowBrush);
            Message = UserMessages.RequestButtonPress;

            _eventAggregator = eventAggregator;
            SwitchStatusReceivedEventSubscriptionToken = eventAggregator.GetEvent<DeviceSwitchStatusReceivedEvent>().Subscribe(ParseSerialNmber);
        }

        #endregion Constructors

        #region Methods

        private void ParseSerialNmber(DeviceSwitchStatusPayload payload)
        {
            if (!string.IsNullOrWhiteSpace(payload.SerialNumber))
            {
                _eventAggregator.GetEvent<DeviceSerialNumberCapturedEvent>().Publish(payload.SerialNumber);
                _eventAggregator.GetEvent<DeviceSwitchStatusReceivedEvent>().Unsubscribe(SwitchStatusReceivedEventSubscriptionToken);
                App.Current.Dispatcher.Invoke(() => RequestClose?.Invoke(null));
            }
        }

        #endregion Methods

        #region Methods

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
        }

        #endregion Methods
    }
}