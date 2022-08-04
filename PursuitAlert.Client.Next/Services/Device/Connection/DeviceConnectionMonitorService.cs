using Prism.Events;
using PursuitAlert.Client.Events;
using PursuitAlert.Client.Properties;
using PursuitAlert.Client.Services.Device.Events;
using PursuitAlert.Client.Services.Device.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.Connection
{
    public class DeviceConnectionMonitorService : IDeviceConnectionMonitorService
    {
        #region Properties

        public SerialPort ConnectedDevice { get; private set; }

        private WqlEventQuery DeviceInsertQuery
        {
            get
            {
                if (deviceInsertQuery == null)
                    deviceInsertQuery = new WqlEventQuery(DeviceInsertQueryText);
                return deviceInsertQuery;
            }
        }

        private ManagementEventWatcher DeviceInsertWatcher
        {
            get
            {
                if (deviceInsertWatcher == null)
                    deviceInsertWatcher = new ManagementEventWatcher(DeviceInsertQuery);
                return deviceInsertWatcher;
            }
        }

        private WqlEventQuery DeviceRemoveQuery
        {
            get
            {
                if (deviceRemoveQuery == null)
                    deviceRemoveQuery = new WqlEventQuery(DeviceRemoveQueryText);
                return deviceRemoveQuery;
            }
        }

        private ManagementEventWatcher DeviceRemoveWatcher
        {
            get
            {
                if (deviceRemoveWatcher == null)
                    deviceRemoveWatcher = new ManagementEventWatcher(DeviceRemoveQuery);
                return deviceRemoveWatcher;
            }
        }

        private WqlEventQuery PowerChangedQuery
        {
            get
            {
                if (powerChangedQuery == null)
                    powerChangedQuery = new WqlEventQuery(PowerChangedQueryText);
                return powerChangedQuery;
            }
        }

        private ManagementEventWatcher PowerChangedWatcher
        {
            get
            {
                if (powerChangedWatcher == null)
                    powerChangedWatcher = new ManagementEventWatcher(PowerChangedQuery);
                return powerChangedWatcher;
            }
        }

        #endregion Properties

        #region Fields

        private const string DeviceInsertQueryText = "SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2";

        private const string DeviceRemoveQueryText = "SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3";

        private const string PowerChangedQueryText = "Win32_PowerManagementEvent";

        private readonly IEventAggregator _eventAggregator;

        private readonly object PortLock = new object();

        private WqlEventQuery deviceInsertQuery;

        private ManagementEventWatcher deviceInsertWatcher;

        private WqlEventQuery deviceRemoveQuery;

        private ManagementEventWatcher deviceRemoveWatcher;

        private bool IsWatchingForDeviceInsertions = false;

        private bool IsWatchingForDeviceRemovals = false;

        private WqlEventQuery powerChangedQuery;

        private ManagementEventWatcher powerChangedWatcher;

        #endregion Fields

        #region Constructors

        public DeviceConnectionMonitorService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            PowerChangedWatcher.EventArrived += HandlePowerChanged;
            PowerChangedWatcher.Start();
        }

        #endregion Constructors

        #region Destructors

        ~DeviceConnectionMonitorService()
        {
            Log.Verbose("Cleaning up management watchers");
            if (PowerChangedWatcher != null)
            {
                try
                {
                    PowerChangedWatcher.EventArrived -= HandlePowerChanged;
                    PowerChangedWatcher.Stop();
                    PowerChangedWatcher.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Verbose("Error trying to clean up PowerChangedWatcher: {message}", ex.Message);
                }
            }

            if (DeviceInsertWatcher != null)
            {
                try
                {
                    StopWatchingForDeviceConnection();
                    DeviceInsertWatcher.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Verbose("Error trying to clean up DeviceInsertWatcher: {message}", ex.Message);
                }
            }

            if (DeviceRemoveWatcher != null)
            {
                try
                {
                    StopWatchingForDeviceDisconnection();
                    DeviceRemoveWatcher.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Verbose("Error trying to clean up DeviceRemoveWatcher: {message}", ex.Message);
                }
            }
        }

        #endregion Destructors

        #region Methods

        public void Disconnect(string reason = "")
        {
            Log.Debug("Disconnecting from device on port {portName}{reason}", ConnectedDevice.PortName, string.IsNullOrWhiteSpace(reason) ? string.Empty : $". {reason}");
            if (ConnectedDevice != null)
            {
                if (ConnectedDevice.IsOpen)
                {
                    Log.Verbose("Closing device connection");
                    ConnectedDevice.Close();
                }
                else
                    Log.Verbose("ConnectedDevice is not open");

                ConnectedDevice.Dispose();
                ConnectedDevice = null;
                Log.Verbose("Device disconnected successfully");
            }
            else
                Log.Warning("ConnectedDevice is null; cannot initiate disconnection");
            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();
        }

        public void StopWatchingForDeviceConnection()
        {
            // Stop watching for connections since the device is presumably already connected
            DeviceInsertWatcher.Stop();
            DeviceInsertWatcher.EventArrived -= HandleDeviceInserted;
            IsWatchingForDeviceInsertions = false;
            Log.Debug("No longer watching for device connections");
        }

        public void StopWatchingForDeviceDisconnection()
        {
            // Stop watching for disconnections since the device is presumably already disconnected
            DeviceRemoveWatcher.EventArrived -= HandleDeviceRemoved;
            DeviceRemoveWatcher.Stop();
            IsWatchingForDeviceRemovals = false;
            Log.Debug("No longer watching for device disconnections");
        }

        public bool TryConnect(out SerialPort connectedDevice)
        {
            var comPorts = ComPortNames.GetComPortNamesForDevice(DeviceConnectionSettings.Default.VID, DeviceConnectionSettings.Default.PID);
            if (comPorts.Count > 0)
            {
                lock (PortLock)
                {
                    // Get the port names of any connected serial devices
                    var portNames = SerialPort.GetPortNames();
                    foreach (var portName in portNames)
                        if (comPorts.Contains(portName))
                        {
                            Log.Verbose("COM port for device with a VID and PID matching a Pursuit Alert device found. Trying to connect");
                            if (TryConnectToPort(portName, out connectedDevice))
                            {
                                ConnectedDevice = connectedDevice;
                                _eventAggregator.GetEvent<DeviceConnectedEvent>().Publish();
                                return true;
                            }
                            else
                                return false;
                        }
                }
                connectedDevice = null;
                return false;
            }
            else
            {
                Log.Verbose("No devices connected with VID {vid} and PID {pid} (No COM ports were found)", DeviceConnectionSettings.Default.VID, DeviceConnectionSettings.Default.PID);
                connectedDevice = null;
                return false;
            }
        }

        public void WatchForDeviceConnection()
        {
            // Start watching for connections only if not already watching for insertions
            if (!IsWatchingForDeviceInsertions)
            {
                DeviceInsertWatcher.EventArrived += HandleDeviceInserted;
                DeviceInsertWatcher.Start();
                IsWatchingForDeviceInsertions = true;
                Log.Debug("Watching for device connections");
            }
        }

        public void WatchForDeviceDisconnection()
        {
            // Start watching for disconnections
            if (!IsWatchingForDeviceRemovals)
            {
                DeviceRemoveWatcher.EventArrived += HandleDeviceRemoved;
                DeviceRemoveWatcher.Start();
                IsWatchingForDeviceRemovals = true;
                Log.Debug("Watching for device removals");
            }
        }

        private void HandleDeviceInserted(object sender, EventArrivedEventArgs e)
        {
            Log.Verbose("Device inserted");

            // Stop watching for connections while attempting to connect
            StopWatchingForDeviceConnection();

            if (TryConnect(out SerialPort device))
            {
                ConnectedDevice = device;

                _eventAggregator.GetEvent<DeviceConnectedEvent>().Publish();
            }
            else
            {
                WatchForDeviceConnection();
            }
        }

        private void HandleDeviceRemoved(object sender, EventArrivedEventArgs e)
        {
            if (e != null && e.NewEvent != null && e.NewEvent.Properties != null && e.NewEvent.Properties.Count > 0)
            {
                var properties = new StringBuilder();
                foreach (var property in e.NewEvent.Properties)
                    properties.Append($"{property.Name}={property.Value}, ");

                Log.Verbose("Device removed: {properties}", properties.ToString().Trim().TrimEnd(','));
            }
            else
            {
                Log.Verbose("Device removed");
            }

            // Stop watching for disconnections while determining if it was a Pursuit Alert device
            // that was disconnected
            StopWatchingForDeviceDisconnection();

            if (!TryConnect(out SerialPort _))
            {
                Log.Information("Pursuit Alert device removed");

                // TODO: logic to determine if the disconnected device was the Pursuit Alert device
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();
            }
            else
            {
                WatchForDeviceDisconnection();
                Log.Verbose("Pursuit Alert device still connected");
            }
        }

        private void HandlePowerChanged(object sender, EventArrivedEventArgs e)
        {
            Log.Verbose("Power change event triggered");

            int eventType = Convert.ToInt32(e.NewEvent.Properties["EventType"].Value);

            switch (eventType)
            {
                case 4:
                    HandleSystemSleep();
                    break;

                case 7:
                    HandleSystemWake();
                    break;

                default:
                    break;
            }
        }

        private void HandleSystemSleep()
        {
            Log.Verbose("System is entering sleep mode");
            _eventAggregator.GetEvent<SystemSleepEvent>().Publish();

            // Stop listening for device changes
            StopWatchingForDeviceDisconnection();
            StopWatchingForDeviceConnection();

            Log.Verbose("------ SYSTEM SLEEP ------\r\n");
        }

        private void HandleSystemWake()
        {
            Log.Verbose("------ SYSTEM WAKE ------");
            Log.Verbose("System is waking up from sleep mode");
            _eventAggregator.GetEvent<SystemWakeEvent>().Publish();

            if (ConnectedDevice != null && !ConnectedDevice.IsOpen)
            {
                // Check if a device is connected, but not open. This is most likely case that
                // causes issues after waking from sleep mode
                Disconnect("Waking from sleep mode");
            }
            else if (ConnectedDevice != null)

                // If a device is connected, start listening for disconnections again
                DeviceRemoveWatcher.Start();
            else

                // If no device is connected, start listening for connections
                WatchForDeviceConnection();
        }

        private bool TryConnectToPort(string portName, out SerialPort connectedPort)
        {
            Log.Verbose("Attempting to connect to device on COM port {portName}", portName);
            connectedPort = new SerialPort(portName)
            {
                BaudRate = DeviceConnectionSettings.Default.BaudRate,
                Parity = (Parity)DeviceConnectionSettings.Default.Parity,
                StopBits = (StopBits)DeviceConnectionSettings.Default.StopBits,
                DataBits = DeviceConnectionSettings.Default.DataBits,
                NewLine = "\r\n",
                ReadTimeout = DeviceConnectionSettings.Default.ReadTimeout,
                WriteTimeout = DeviceConnectionSettings.Default.WriteTimeout
            };
            try
            {
                string portInfo = connectedPort.BaudRate.ToString() + ", " + connectedPort.Parity.ToString() + ", "
                                  + connectedPort.StopBits.ToString() + ", " + connectedPort.DataBits.ToString() +
                                  ", " + connectedPort.NewLine.ToString() + ", "
                                  + connectedPort.ReadTimeout.ToString() + ", " + connectedPort.WriteTimeout.ToString();
                Log.Verbose("Connected Port: {portInfo}", portInfo);
                if (!connectedPort.IsOpen)
                {
                    Log.Verbose("Opening connection to device on COM port {portName}", portName);
                    connectedPort.Open();
                    Log.Verbose("Successfully opened connection to device on COM port {portName}", portName);
                }
                Log.Verbose("Successfully connected to device on COM port {portName}", portName);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Verbose("Received {exceptionType} when trying to connect to device on port {portName}; assuming device is already connected", ex.GetType().Name, portName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Verbose("Failed to connect to device on COM port {portName}: {message}", portName, ex.Message);
                return false;
            }
        }

        #endregion Methods
    }
}