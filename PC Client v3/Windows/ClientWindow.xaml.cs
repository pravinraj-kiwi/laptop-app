using Prism.Events;
using PursuitAlert.Domain.Modes.Events;
using PursuitAlert.Domain.Modes.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PursuitAlert.Client.Old.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ClientWindow : Window
    {
        #region Fields

        private readonly IEventAggregator _eventAggregator;
        private Mode _delayedMode;
        private TimeSpan _time;
        private DispatcherTimer _timer;

        #endregion Fields

        #region Constructors

        public ClientWindow(IEventAggregator eventAggregator)
        {
            InitializeComponent();
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<DelayedModeRequestedEvent>().Subscribe(HandleCountdown);
            _eventAggregator.GetEvent<DelayedModeCancelRequestedEvent>().Subscribe(HandleCountdownCancel);
            _eventAggregator.GetEvent<PinDroppedEvent>().Subscribe(marker => beginAnimation("PinDropped"));
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// https://stackoverflow.com/a/16749451
        /// </summary>
        /// <param name="sender">
        /// </param>
        /// <param name="e">
        /// </param>
        private void AnimateCountdown(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                CountdownTime.FontSize = 72;
                CountdownTime.Text = _time.Seconds.ToString();
                if (_time == TimeSpan.Zero)
                {
                    // The mode has been enabled
                    _eventAggregator.GetEvent<DelayedModeEngagedEvent>().Publish(_delayedMode);
                    _delayedMode = null;
                    _timer.Stop();
                    return;
                }
                _eventAggregator.GetEvent<DelayedModeCountdownTimerTick>().Publish(_delayedMode);
                beginAnimation("CountdownTimer");
                _time = _time.Add(TimeSpan.FromSeconds(-1));
            });
        }

        private void beginAnimation(string animationName)
        {
            Dispatcher.Invoke(() =>
            {
                var animation = System.Windows.Application.Current.MainWindow.FindResource(animationName) as Storyboard;
                animation.Begin();
            });
        }

        private void HandleCountdown(Mode mode)
        {
            _delayedMode = mode;
            _time = TimeSpan.FromSeconds(mode.IncidentDelay);
            // Start the first frame of the animation
            App.Current.Dispatcher.Invoke(() =>
            {
                CountdownTime.FontSize = 72;
                CountdownTime.Text = _time.Seconds.ToString();
                _eventAggregator.GetEvent<DelayedModeCountdownTimerTick>().Publish(_delayedMode);
                beginAnimation("CountdownTimer");
            });
            _time = _time.Add(TimeSpan.FromSeconds(-1));
            _timer = new DispatcherTimer(
                new TimeSpan(0, 0, 1),
                DispatcherPriority.Normal,
                AnimateCountdown,
                App.Current.Dispatcher);
            _timer.Start();
        }

        private void HandleCountdownCancel(Mode mode)
        {
            Dispatcher.Invoke(() =>
            {
                CountdownTime.FontSize = 24;
                CountdownTime.Text = "Canceled";
                beginAnimation("CountdownTimer");
                _timer.Stop();
            });
            Task.Run(() =>
            {
                Task.Delay(1000).Wait();
                _eventAggregator.GetEvent<DelayedModeCanceledEvent>().Publish(mode);
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var desktopWorkingArea = SystemParameters.WorkArea;
            Left = desktopWorkingArea.Right - Width - 10;
            Top = desktopWorkingArea.Bottom - Height;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        #endregion Methods
    }
}