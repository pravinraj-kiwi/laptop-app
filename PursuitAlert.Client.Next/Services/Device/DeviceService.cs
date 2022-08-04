using Prism.Events;
using PursuitAlert.Client.Dialogs.SettingsDialog;
using PursuitAlert.Client.Events;
using PursuitAlert.Client.Infrastructure.API;
using PursuitAlert.Client.Infrastructure.IoTManagement;
using PursuitAlert.Client.Properties;
using PursuitAlert.Client.Services.Device.Connection;
using PursuitAlert.Client.Services.Device.Errors;
using PursuitAlert.Client.Services.Device.Events;
using PursuitAlert.Client.Services.Device.LED;
using PursuitAlert.Client.Services.Device.Payloads;
using PursuitAlert.Client.Services.Modes;
using PursuitAlert.Client.Services.Modes.Events;
using PursuitAlert.Client.Services.Modes.Events.EventPayloads;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device
{
    public class DeviceService : IDeviceService
    {
        #region Properties

        public SerialPort Device { get; private set; }

        public bool IsDeviceConnected => Device != null;

        public bool IsDeviceInitialized { get; private set; }

        public string SerialNumber { get; private set; }

        #endregion Properties

        #region Fields

        private readonly IAPIService _api;

        private readonly IDeviceConnectionMonitorService _connectionMonitorService;

        private readonly IEventAggregator _eventAggregator;

        private readonly ILEDService _ledService;

        private readonly IPayloadService _payloadService;

        #endregion Fields

        #region Constructors

        public DeviceService(IDeviceConnectionMonitorService connectionMonitorService,
            ILEDService ledService,
            IPayloadService payloadService,
            IAPIService api,
            IIoTManagementService ioTManagementService,
            IEventAggregator eventAggregator)
        {
            _connectionMonitorService = connectionMonitorService;
            _ledService = ledService;
            _payloadService = payloadService;
            _api = api;
            _eventAggregator = eventAggregator;

            if (!_eventAggregator.GetEvent<SystemSleepEvent>().Contains(HandleSystemSleep))
                _eventAggregator.GetEvent<SystemSleepEvent>().Subscribe(HandleSystemSleep);
        }

        #endregion Constructors

        #region Methods

        public void CloseConnection(string reason = "")
        {
            if (Device == null)
                Log.Information("Closing connections; device disconnected{reason}", string.IsNullOrWhiteSpace(reason) ? string.Empty : $". {reason}");
            else
            {
                Log.Information("Closing connection to device on port {portName}{reason}", Device.PortName, string.IsNullOrWhiteSpace(reason) ? string.Empty : $". {reason}");
                StopListening();
                if (IsDeviceConnected)
                    _ledService.TurnOffAllLEDs(Device);
                _connectionMonitorService.Disconnect(reason);
                Device = null;
            }
        }

        public void Initialize()
        {
            // Check to see if a device is already connected or try to connect to a device
            if (_connectionMonitorService.ConnectedDevice != null || _connectionMonitorService.TryConnect(out SerialPort device))
                HandleDeviceConnection();
            else
                HandleDeviceDisconnection();
        }

        public void StopListening() => _payloadService.StopListening();

        private void HandleDelayedModeTimerTick(DelayedModeTimerTickEventPayload tick)
        {
            if (IsDeviceConnected)
                _ledService.ShortFlashLED(Device, tick.Mode.ButtonPosition, 0.25);
            else
                Log.Debug("Requested LED short flash, however no device is connected");
        }

        private void HandleDeviceConnection()
        {
            Device = _connectionMonitorService.ConnectedDevice;

            // Tell the connection monitor to watch for device disconnections and let us know when
            // the device disconnects
            _connectionMonitorService.WatchForDeviceDisconnection();

            // Listen for settings changes and force a disconnect when device settings are changed
            if (!_eventAggregator.GetEvent<DeviceSettingsChangedEvent>().Contains(HandleSettingsChanged))
                _eventAggregator.GetEvent<DeviceSettingsChangedEvent>().Subscribe(HandleSettingsChanged);

            // Don't listen for device connections anymore since a device is connected
            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceConnection))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceConnection);

            // Listen for device disconnections
            if (!_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleDeviceDisconnection))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleDeviceDisconnection);

            // Listen for mode changes to light buttons as necessary
            if (!_eventAggregator.GetEvent<ModeChangeEvent>().Contains(HandleModeChange))
                _eventAggregator.GetEvent<ModeChangeEvent>().Subscribe(HandleModeChange);

            // Handle delayed mode timer countdown
            if (!_eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Contains(HandleDelayedModeTimerTick))
                _eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Subscribe(HandleDelayedModeTimerTick);

            // Handle pin drop
            if (!_eventAggregator.GetEvent<PinDroppedEvent>().Contains(HandlePinDropped))
                _eventAggregator.GetEvent<PinDroppedEvent>().Subscribe(HandlePinDropped);

            // Get the serial number
            try
            {
                SerialNumber = _payloadService.GetDeviceSerialNumber(Device);

                if (string.IsNullOrWhiteSpace(Settings.Default.DeviceSerialNumber))
                {
                    Log.Verbose("Serial number stored in settings is empty; serial number retrieved from device: {serialNumber}", SerialNumber);

                    // If no serial number was set in the settings, this appears to be a new
                    // installation and potentially a new device. Make sure the serail number exists
                    // as a node in the database
                    var deviceSetupAssuranceTask = _api.EnsureDeviceNodeIsProperlyConfigured(SerialNumber);
                    try
                    {
                        deviceSetupAssuranceTask.Wait();
                        if (deviceSetupAssuranceTask.Exception != null)
                            throw deviceSetupAssuranceTask.Exception;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to ensure the device was set up properly");
                        throw ex;
                    }
                    var mapping = _api.EnsureAssetDeviceAssociation(VehicleSettings.Default.UnitId, VehicleSettings.Default.Id, SerialNumber).Result;

                    // ! If the mapping comes back null, there was a problem that should have
                    // resulted in a dialog being shown to the user. Stop initialization. The
                    // resulting dialog should take care of re-initializing.
                    if (mapping == null)
                        return;

                    Log.Information("Saving serial number in settings: {serialNumber}", SerialNumber);
                    Settings.Default.DeviceSerialNumber = SerialNumber;
                    Settings.Default.Save();
                }
                else if (!Settings.Default.DeviceSerialNumber.Equals(SerialNumber, StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.Information("New device detected: serial number stored in settings ({existingSerialNumber}) does not match serial number retrieved from device: {serialNumber}", Settings.Default.DeviceSerialNumber, SerialNumber);

                    // If the serial number is different, this may be a new device, so make sure it
                    // exists and ensure there's an association bewteen this device and this vehicle
                    Log.Information("New serial number found on device");
                    var deviceSetupAssuranceTask = _api.EnsureDeviceNodeIsProperlyConfigured(SerialNumber);
                    try
                    {
                        deviceSetupAssuranceTask.Wait();
                        if (deviceSetupAssuranceTask.Exception != null)
                            throw deviceSetupAssuranceTask.Exception;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to ensure the device was set up properly");
                        throw ex;
                    }
                    _api.EnsureAssetDeviceAssociation(VehicleSettings.Default.UnitId, VehicleSettings.Default.Id, SerialNumber).Wait();
                    Log.Information("Saving serial number in settings: {serialNumber}", SerialNumber);
                    Settings.Default.DeviceSerialNumber = SerialNumber;
                    Settings.Default.Save();
                }
                else
                {
                    Log.Information("Matching serial number found in settings ({existingSerialNumber}) as was retrieved from the device: {serialNumber}", Settings.Default.DeviceSerialNumber, SerialNumber);
                    var deviceSetupAssuranceTask = _api.EnsureDeviceNodeIsProperlyConfigured(SerialNumber);
                    try
                    {
                        deviceSetupAssuranceTask.Wait();
                        if (deviceSetupAssuranceTask.Exception != null)
                            throw deviceSetupAssuranceTask.Exception;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to ensure the device was set up properly");
                        throw ex;
                    }
                    _api.EnsureAssetDeviceAssociation(VehicleSettings.Default.UnitId, VehicleSettings.Default.Id, SerialNumber).Wait();
                }
            }
            catch (DeviceReadException ex)
            {
                Log.Error(ex, "An error ocurred while trying to get the serial number from the device on port {portName}", Device.PortName);
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();
                return;
            }

            // Start listening to what the device says
            try
            {
                _payloadService.ListenToDevice(Device);
            }
            catch (DeviceReadException ex)
            {
                Log.Error(ex, "Failed to start listening to the device on port {portName}", Device.PortName);
                CloseConnection("An error occurred trying to listen to the device");
                return;
            }

            // Turn on the LEDs
            try
            {
                _ledService.SetInitialState(Device);
            }
            catch (DeviceWriteException ex)
            {
                Log.Error(ex, "Failed to send initial LED state to the device on port {portName}", Device.PortName);
                CloseConnection("An error ocurred trying to send the initial LED paylod to the device");
                return;
            }

            IsDeviceInitialized = true;
        }

        private void HandleDeviceDisconnection()
        {
            Device = null;

            // Unset the serial number
            SerialNumber = string.Empty;

            // Tell the connection monitor to watch for device conenctions and let us know when a
            // device connects
            _connectionMonitorService.WatchForDeviceConnection();

            // Stop listening for settings changes
            if (_eventAggregator.GetEvent<DeviceSettingsChangedEvent>().Contains(HandleSettingsChanged))
                _eventAggregator.GetEvent<DeviceSettingsChangedEvent>().Unsubscribe(HandleSettingsChanged);

            // Stop listening for mode changes
            if (_eventAggregator.GetEvent<ModeChangeEvent>().Contains(HandleModeChange))
                _eventAggregator.GetEvent<ModeChangeEvent>().Unsubscribe(HandleModeChange);

            // Don't listen for disconnections anymore since the device just disconnected; it can
            // only disconnect once
            if (_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleDeviceDisconnection))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(HandleDeviceDisconnection);

            // Listen for device connections
            if (!_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceConnection))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceConnection);

            // Unhandle delayed mode timer countdown
            if (_eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Contains(HandleDelayedModeTimerTick))
                _eventAggregator.GetEvent<DelayedModeTimerTickEvent>().Unsubscribe(HandleDelayedModeTimerTick);

            // Unhandle pin drops
            if (_eventAggregator.GetEvent<PinDroppedEvent>().Contains(HandlePinDropped))
                _eventAggregator.GetEvent<PinDroppedEvent>().Unsubscribe(HandlePinDropped);
        }

        private void HandleModeChange(ModeChangeEventPayload modeChange)
        {
            if (modeChange.ChangeType == ModeChangeType.Engaged)
                _ledService.BeginFlashing(Device, modeChange.NewMode.ButtonPosition);
            else if (modeChange.ChangeType == ModeChangeType.Disengaged)
                _ledService.StopFlashing(Device, modeChange.NewMode.ButtonPosition);
        }

        private void HandlePinDropped(Mode pinDropMode)
        {
            _ledService.ShortFlashLED(Device, pinDropMode.ButtonPosition);
        }

        private void HandleSettingsChanged()
        {
            CloseConnection("Device settings have been changed");
            Initialize();
        }

        private void HandleSystemSleep()
        {
            StopListening();
            if (IsDeviceConnected)
                _ledService.TurnOffAllLEDs(Device);

            if (!_eventAggregator.GetEvent<SystemWakeEvent>().Contains(HandleSystemWake))
                _eventAggregator.GetEvent<SystemWakeEvent>().Subscribe(HandleSystemWake);

            if (_eventAggregator.GetEvent<SystemSleepEvent>().Contains(HandleSystemSleep))
                _eventAggregator.GetEvent<SystemSleepEvent>().Unsubscribe(HandleSystemSleep);
        }

        private void HandleSystemWake()
        {
            if (IsDeviceConnected)
            {
                _ledService.SetInitialState(Device);
                _payloadService.ListenToDevice(Device);
            }

            if (!_eventAggregator.GetEvent<SystemSleepEvent>().Contains(HandleSystemSleep))
                _eventAggregator.GetEvent<SystemSleepEvent>().Subscribe(HandleSystemSleep);

            if (_eventAggregator.GetEvent<SystemWakeEvent>().Contains(HandleSystemWake))
                _eventAggregator.GetEvent<SystemWakeEvent>().Unsubscribe(HandleSystemWake);
        }

        #endregion Methods
    }
}