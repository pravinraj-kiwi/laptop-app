using FontAwesome.WPF;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PursuitAlert.Client.Old.Dialogs.NewDeviceConnected
{
    public class NewDeviceConnectedDialogViewModel : BindableBase, IDialogAware
    {
        #region Properties

        public DelegateCommand<string> CloseWithResult { get; }

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
            get => message;
            set => SetProperty(ref message, value);
        }

        public string NewSerialNumber
        {
            get => newSerialNumber;
            set => SetProperty(ref newSerialNumber, value);
        }

        public string PreviousSerialNumber
        {
            get => previousSerialNumber;
            set => SetProperty(ref previousSerialNumber, value);
        }

        public string Title => "New device detected";

        #endregion Properties

        #region Fields

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            NewSerialNumber = parameters.GetValue<string>("newSerial");
            PreviousSerialNumber = parameters.GetValue<string>("previousSerial");
        }

        private const string MoveOverYellowBrush = "MoveOverYellowBrush";

        private string _icon;

        private SolidColorBrush _iconForeground;

        private string message = "A new device has been connected. This device will have to go through a re-authentication process with the server. The previous device needs to be deactivated before it can be used again.";

        private string newSerialNumber;

        private string previousSerialNumber;

        #endregion Fields

        #region Constructors

        public NewDeviceConnectedDialogViewModel()
        {
            Icon = FontAwesomeIcon.ExclamationTriangle.ToString();
            IconForeground = (SolidColorBrush)System.Windows.Application.Current.FindResource(MoveOverYellowBrush);

            CloseWithResult = new DelegateCommand<string>(CloseWithOK);
        }

        #endregion Constructors

        #region Methods

        private void CloseWithOK(string parameter)
        {
            ButtonResult result = ButtonResult.None;

            if (parameter.ToLower().Equals("true"))
                result = ButtonResult.OK;
            else
                result = ButtonResult.Cancel;

            RequestClose?.Invoke(new DialogResult(result));
        }

        #endregion Methods
    }
}