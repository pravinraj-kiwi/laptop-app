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

namespace PursuitAlert.Client.Dialogs.SettingsDialog
{
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : UserControl
    {
        #region Constructors

        public SettingsDialog()
        {
            InitializeComponent();
            TitleBar.MouseLeftButtonDown += Drag;
        }

        #endregion Constructors

        #region Methods

        private void Drag(object sender, MouseButtonEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
                window.DragMove();
        }

        #endregion Methods
    }
}