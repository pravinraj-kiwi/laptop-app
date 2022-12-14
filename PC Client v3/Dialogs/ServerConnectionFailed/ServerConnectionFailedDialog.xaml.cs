using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PursuitAlert.Client.Old.Dialogs.ServerConnectionFailed
{
    /// <summary>
    /// Interaction logic for ServerConnectionFailedDialog.xaml
    /// </summary>
    public partial class ServerConnectionFailedDialog : UserControl
    {
        #region Constructors

        public ServerConnectionFailedDialog()
        {
            InitializeComponent();
            Close.Click += (s, e) => (Parent as Window).Close();
        }

        #endregion Constructors

        #region Methods

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                (Parent as Window).DragMove();
        }

        #endregion Methods
    }
}