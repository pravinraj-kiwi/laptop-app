using FontAwesome.WPF;
using Prism.Events;
using Prism.Mvvm;
using PursuitAlert.Domain.Application.Events;
using PursuitAlert.Domain.Device.Events;
using PursuitAlert.Domain.Device.Payloads.Events;
using PursuitAlert.Domain.Device.Payloads.Models;
using PursuitAlert.Domain.Modes.Events;
using PursuitAlert.Domain.Publishing.Events;
using System;
using System.IO.Ports;
using System.Windows.Media;

namespace PursuitAlert.Client.Old.States
{
    public class CurrentStateViewModel : BindableBase
    {
        #region Properties

        public FontAwesomeIcon GPSFixIcon
        {
            get => _gpsIcon;
            set => SetProperty(ref _gpsIcon, value);
        }

        public SolidColorBrush GPSFixIconForeground
        {
            get => _gpsFixIconForeground;
            set => SetProperty(ref _gpsFixIconForeground, value);
        }

        public string GPSState
        {
            get => _gpsState;
            set => SetProperty(ref _gpsState, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public SolidColorBrush IconForeground
        {
            get => _iconForeground;
            set => SetProperty(ref _iconForeground, value);
        }

        public bool IsBrokerConnected { get; private set; }

        public FontAwesomeIcon PatrolIcon
        {
            get => _patrolIcon;
            set => SetProperty(ref _patrolIcon, value);
        }

        public SolidColorBrush PatrolIconForeground
        {
            get => _patrolIconForeground;
            set => SetProperty(ref _patrolIconForeground, value);
        }

        public string PatrolState
        {
            get => _patrolState;
            set => SetProperty(ref _patrolState, value);
        }

        public string SerialNumber
        {
            get => _serialNumber;
            set => SetProperty(ref _serialNumber, value);
        }

        public string State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        #endregion Properties

        #region Fields

        private const string CriticalGreenBrush = "CriticalGreenBrush";

        private const string EmergencyRedBrush = "EmergencyRedBrush";

        private const string LightBrush = "LightBrush";

        private const string MoveOverYellowBrush = "MoveOverYellowBrush";

        private const decimal TotalSatelliteStrength = 12m;

        private readonly IEventAggregator _eventAggregator;

        private SolidColorBrush _gpsFixIconForeground;

        private FontAwesomeIcon _gpsIcon;

        private string _gpsState;

        private string _icon;

        private SolidColorBrush _iconForeground;

        private FontAwesomeIcon _patrolIcon;

        private SolidColorBrush _patrolIconForeground;

        private string _patrolState;

        private string _serialNumber;

        private string _state;

        #endregion Fields

        #region Constructors

        public CurrentStateViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            SetState("Initializing", FontAwesomeIcon.CircleThin, LightBrush);
            SetPatrolState("Not Patrolling", FontAwesomeIcon.CircleThin, EmergencyRedBrush);
            SetGPSState("GPS Initializing", FontAwesomeIcon.CircleThin, LightBrush);
            StartEventSubscriptions();
        }

        #endregion Constructors

        #region Methods

        private int CalculateSignalStrength(int strength)
        {
            decimal ratio = strength / TotalSatelliteStrength * 3m;
            var signal = Math.Round(ratio, MidpointRounding.ToEven);
            return Convert.ToInt32(signal);
        }

        private void HandleApplicationExit(int exitCode)
        {
            // Device initialization events
            _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceConnected);
            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(HandleDeviceDisconnected);
            _eventAggregator.GetEvent<NoDeviceFoundEvent>().Unsubscribe(HandleNoDeviceFound);
            _eventAggregator.GetEvent<DeviceErrorEvent>().Unsubscribe(HandleDeviceError);

            // Mqtt initialization events
            _eventAggregator.GetEvent<BrokerConnectedEvent>().Unsubscribe(HandleBrokerConnected);
            _eventAggregator.GetEvent<BrokerDisconnectedEvent>().Unsubscribe(HandleBrokerDisconnected);

            // Patrol state events
            _eventAggregator.GetEvent<PatrolStartedEvent>().Unsubscribe(HandlePatrolStarted);
            _eventAggregator.GetEvent<PatrolEndedEvent>().Unsubscribe(HandlePatrolEnded);

            // GPS Signal events
            _eventAggregator.GetEvent<CoordinatesPayloadReceivedEvent>().Unsubscribe(HandleCoordinatePayload);

            _eventAggregator.GetEvent<ApplicationExitEvent>().Unsubscribe(HandleApplicationExit);
        }

        private void HandleBrokerConnected(string brokerUri)
        {
            SetState("Connected", FontAwesomeIcon.Check, LightBrush);
            HandlePatrolStarted();
            IsBrokerConnected = true;
        }

        private void HandleBrokerDisconnected(string disconnectionReason)
        {
            SetState("Server disconnected", FontAwesomeIcon.ChainBroken, EmergencyRedBrush);
            IsBrokerConnected = false;
        }

        private void HandleCoordinatePayload(DeviceCoordinatesPayload payload)
        {
            if (payload.SatelliteCount == 0
                || double.IsNaN(payload.Longitude)
                || double.IsNaN(payload.Latitude))
                SetGPSState("GPS Signal Lost", FontAwesomeIcon.Globe, MoveOverYellowBrush);
            else
                SetGPSState("GPS Connected", FontAwesomeIcon.Globe, LightBrush);
        }

        private void HandleDeviceBackOnline()
        {
            HandleBrokerConnected(string.Empty);
            HandlePatrolStarted();
        }

        private void HandleDeviceConnected(SerialPort device)
        {
            SetState("Device connected", FontAwesomeIcon.Usb, LightBrush);

            if (IsBrokerConnected)
            {
                HandleBrokerConnected(string.Empty);
                HandlePatrolStarted();
            }
            else
            {
                HandleBrokerDisconnected(string.Empty);
                HandlePatrolEnded();
            }

            if (GPSState.Equals("No Signal"))
                SetGPSState("GPS Initializing", FontAwesomeIcon.CircleThin, LightBrush);

            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceConnected))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceConnected);
        }

        private void HandleDeviceDisconnected()
        {
            SetState("Device disconnected", FontAwesomeIcon.ChainBroken, EmergencyRedBrush);
            SetGPSState("No Signal", FontAwesomeIcon.Ban, LightBrush);
            HandlePatrolEnded();

            if (!_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceConnected))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceConnected);
        }

        private void HandleDeviceError(Exception obj)
        {
            SetState("Device error", FontAwesomeIcon.Exclamation, EmergencyRedBrush);
            SetGPSState("No Signal", FontAwesomeIcon.Ban, LightBrush);
        }

        private void HandleDeviceOffline()
        {
            HandleBrokerDisconnected("Device offline");

            if (!_eventAggregator.GetEvent<OnlineEvent>().Contains(HandleDeviceBackOnline))
                _eventAggregator.GetEvent<OnlineEvent>().Subscribe(HandleDeviceBackOnline);
        }

        private void HandleNoDeviceFound()
        {
            SetState("No device found", FontAwesomeIcon.Exclamation, MoveOverYellowBrush);
            SetGPSState("No Signal", FontAwesomeIcon.Ban, LightBrush);
        }

        private void HandlePatrolEnded()
        {
            SetPatrolState("Not Patrolling", FontAwesomeIcon.CircleOutline, EmergencyRedBrush);
            if (_eventAggregator.GetEvent<PatrolEndedEvent>().Contains(HandlePatrolEnded))
                _eventAggregator.GetEvent<PatrolEndedEvent>().Unsubscribe(HandlePatrolEnded);
            if (!_eventAggregator.GetEvent<PatrolStartedEvent>().Contains(HandlePatrolStarted))
                _eventAggregator.GetEvent<PatrolStartedEvent>().Subscribe(HandlePatrolStarted);
        }

        private void HandlePatrolStarted()
        {
            SetPatrolState("On Patrol", FontAwesomeIcon.Circle, CriticalGreenBrush);
            if (_eventAggregator.GetEvent<PatrolStartedEvent>().Contains(HandlePatrolStarted))
                _eventAggregator.GetEvent<PatrolStartedEvent>().Unsubscribe(HandlePatrolStarted);
            if (!_eventAggregator.GetEvent<PatrolEndedEvent>().Contains(HandlePatrolEnded))
                _eventAggregator.GetEvent<PatrolEndedEvent>().Subscribe(HandlePatrolEnded);
        }

        private void HandleSerialNumberCaptured(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(SerialNumber) || SerialNumber != serialNumber)
                SerialNumber = serialNumber;
        }

        private void SetGPSState(string state, FontAwesomeIcon icon, string iconForeground)
        {
            GPSState = state;
            GPSFixIcon = icon;
            GPSFixIconForeground = (SolidColorBrush)App.Current.FindResource(iconForeground);
        }

        private void SetPatrolState(string patrolState, FontAwesomeIcon icon, string patrolIconForeground = LightBrush)
        {
            PatrolState = patrolState;
            PatrolIcon = icon;
            PatrolIconForeground = (SolidColorBrush)App.Current.FindResource(patrolIconForeground);
        }

        private void SetState(string state, FontAwesomeIcon icon, string brush = LightBrush)
        {
            State = state;
            Icon = icon.ToString();
            IconForeground = (SolidColorBrush)App.Current.FindResource(brush);
        }

        private void StartEventSubscriptions()
        {
            // Device initialization events
            _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceConnected);
            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleDeviceDisconnected);
            _eventAggregator.GetEvent<NoDeviceFoundEvent>().Subscribe(HandleNoDeviceFound);
            _eventAggregator.GetEvent<DeviceErrorEvent>().Subscribe(HandleDeviceError);

            // Mqtt initialization events
            _eventAggregator.GetEvent<BrokerConnectedEvent>().Subscribe(HandleBrokerConnected);
            _eventAggregator.GetEvent<BrokerDisconnectedEvent>().Subscribe(HandleBrokerDisconnected);
            _eventAggregator.GetEvent<OfflineEvent>().Subscribe(HandleDeviceOffline);

            // Patrol state events
            _eventAggregator.GetEvent<PatrolStartedEvent>().Subscribe(HandlePatrolStarted);
            _eventAggregator.GetEvent<PatrolEndedEvent>().Subscribe(HandlePatrolEnded);

            // GPS Signal events
            _eventAggregator.GetEvent<CoordinatesPayloadReceivedEvent>().Subscribe(HandleCoordinatePayload);

            _eventAggregator.GetEvent<DeviceSerialNumberCapturedEvent>().Subscribe(HandleSerialNumberCaptured);

            _eventAggregator.GetEvent<ApplicationExitEvent>().Subscribe(HandleApplicationExit, ThreadOption.PublisherThread);
        }

        #endregion Methods
    }
}