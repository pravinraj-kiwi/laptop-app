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

namespace PursuitAlert.Client.Old.Dialogs.ServerConnectionFailed
{
    public class ServerConnectionFailedDialogViewModel : BindableBase, IDialogAware
    {
        #region Properties

        public event Action<IDialogResult> RequestClose;

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

        public string Title => "Server Connection Failed";

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
        }

        #endregion Properties

        #region Fields

        private const string MoveOverYellowBrush = "MoveOverYellowBrush";

        private string _icon;

        private SolidColorBrush _iconForeground;

        private string message = "Failed to connect to the server. Please contact your administrator to have this device reset so that it can re-authenticate with the server.";

        private string newSerialNumber;

        private string previousSerialNumber;

        #endregion Fields

        #region Constructors

        public ServerConnectionFailedDialogViewModel()
        {
            Icon = FontAwesomeIcon.ExclamationTriangle.ToString();
            IconForeground = (SolidColorBrush)System.Windows.Application.Current.FindResource(MoveOverYellowBrush);
        }

        #endregion Constructors
    }
}