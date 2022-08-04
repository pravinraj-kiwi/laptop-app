using Microsoft.Win32;
using Prism.Events;
using PursuitAlert.Domain.Application.Events;
using PursuitAlert.Domain.Device.Events;
using PursuitAlert.Domain.Device.Models;
using PursuitAlert.Domain.Device.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PursuitAlert.Application.Device.Services
{
    public class DeviceMonitorService : IDeviceMonitorService
    {
        #region Properties

        public bool DeviceIsConnected { get; private set; }

        public DeviceConnectionParameters DeviceSettings { get; private set; }

        #endregion Properties

        #region Fields

        private const string DeviceParamtersKey = "Device Parameters";

        private const string DeviceRegExKey = "^VID_{0}.PID_{1}";

        private const string DeviceRemovalQuery = "SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3";

        private const string Name = "PortName";

        private const string RegistryKeyNameKey = "SYSTEM\\CurrentControlSet\\Enum";

        private const string USBRegistryKey = "USB";

        private static ManagementEventWatcher DeviceRemovalWatcher;

        private readonly IEventAggregator _eventAggregator;

        private readonly object _lockObject = new object();

        private Timer _timer;

        private List<string> ComPortNames;

        private bool ListenerEventJitter = false;

        private int ListenerEventJitterTimeoutInMS = 1500;

        private int ListenerInterval;

        private SerialPort SerialPort;

        #endregion Fields

        #region Constructors

        public DeviceMonitorService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            var removalQuery = new WqlEventQuery(DeviceRemovalQuery);
            DeviceRemovalWatcher = new ManagementEventWatcher(removalQuery);
            DeviceRemovalWatcher.EventArrived += HandleDeviceRemoval;
            DeviceRemovalWatcher.Start();

            _eventAggregator.GetEvent<ApplicationExitEvent>().Subscribe(exitCode => Dispose());
        }

        #endregion Constructors

        #region Methods

        public void Dispose()
        {
            StopDeviceListener();

            CloseDevice("Disposing DeviceMonitorService instance");

            if (DeviceRemovalWatcher != null)
                DeviceRemovalWatcher.EventArrived -= HandleDeviceRemoval;

            if (SerialPort != null)
            {
                if (SerialPort.IsOpen)
                    SerialPort.Close();
                SerialPort.Dispose();
            }

            if (_timer != null)
                _timer.Dispose();

            _eventAggregator.GetEvent<ApplicationExitEvent>().Subscribe(exitCode => Dispose());
        }

        /// <summary>
        /// Starts a <see cref="Timer" /> that listens for a device matching the provided PID and
        /// VID in the parameters.
        /// </summary>
        /// <param name="parameters">The parameters of the device to listen for.</param>
        /// <param name="listenerInterval">The interval for the timer.</param>
        public void StartDeviceListener(DeviceConnectionParameters parameters, int listenerInterval = 3000)
        {
            DeviceSettings = parameters;
            ListenerInterval = listenerInterval;

            Log.Debug("Finding COM port names");
            ComPortNames = GetComPortNames(DeviceSettings.VID, DeviceSettings.PID);

            Log.Debug("Starting device listener");
            _timer = new Timer(new TimerCallback(Listen), null, 0, listenerInterval);
        }

        public void StopDeviceListener()
        {
            Log.Debug("Stopping device listener");
            if (_timer == null)
                _timer = new Timer(new TimerCallback(Listen), null, Timeout.Infinite, Timeout.Infinite);
            else
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void CloseDevice(string reason = "")
        {
            Log.Debug("Closing device (Reason = {0})", reason);
            lock (_lockObject)
            {
                if (SerialPort != null)
                {
                    if (SerialPort.IsOpen)
                        SerialPort.Close();
                    SerialPort.Dispose();
                }
            }
            Log.Debug("Device closed");
        }

        /// <summary>
        /// Compile an array of COM port names associated with given VID and PID. Taken from https://www.codeproject.com/Tips/349002/Select-a-USB-Serial-Device-via-its-VID-PID
        /// </summary>
        /// <param name="VID"></param>
        /// <param name="PID"></param>
        /// <returns></returns>
        private List<string> GetComPortNames(string VID, string PID)
        {
            var pattern = string.Format(DeviceRegExKey, VID, PID);
            var _rx = new Regex(pattern, RegexOptions.IgnoreCase);
            var comports = new List<string>();
            var rk1 = Registry.LocalMachine;
            var rk2 = rk1.OpenSubKey(RegistryKeyNameKey);
            var rk3 = rk2.OpenSubKey(USBRegistryKey);
            foreach (var s in rk3.GetSubKeyNames())
            {
                if (_rx.Match(s).Success)
                {
                    var rk4 = rk3.OpenSubKey(s);
                    foreach (var s2 in rk4.GetSubKeyNames())
                    {
                        var rk5 = rk4.OpenSubKey(s2);
                        var rk6 = rk5.OpenSubKey(DeviceParamtersKey);
                        comports.Add((string)rk6.GetValue(Name));
                    }
                }
            }
            return comports;
        }

        private void HandleDeviceRemoval(object sender, EventArrivedEventArgs e)
        {
            try
            {
                // If as of yet, we're communicating with the serial port, try to reconnect and see
                // if we get an error, if we do, the device is most likely disconnected
                if (SerialPort != null)
                    lock (_lockObject)
                        if (!SerialPort.IsOpen)
                            SerialPort.Open();
            }
            catch (Exception)
            {
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();

                if (SerialPort != null)
                {
                    lock (_lockObject)
                    {
                        SerialPort.Close();
                        SerialPort.Dispose();
                    }
                }

                // Start looking for the device again
                StartDeviceListener(DeviceSettings);
            }
        }

        /// <summary>
        /// Checks for a device to be connected every 3 seconds
        /// </summary>
        /// <param name="state"></param>
        private void Listen(object state = null)
        {
            if (ComPortNames.Count > 0)
            {
                lock (_lockObject)
                {
                    var portNames = SerialPort.GetPortNames();
                    foreach (var serialPortName in portNames)
                        if (ComPortNames.Contains(serialPortName))
                        {
                            // Create the serial port and emit the connection event
                            Log.Information("Pursuit alert device found");
                            RaiseDeviceFoundEvent(serialPortName);
                            if (SerialPort != null && SerialPort.IsOpen)
                            {
                                // We're already connected
                                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                                RaiseDeviceConnectedEvent();
                                return;
                            }

                            SerialPort = new SerialPort(serialPortName)
                            {
                                BaudRate = DeviceSettings.BaudRate,
                                Parity = (Parity)DeviceSettings.Parity,
                                StopBits = (StopBits)DeviceSettings.StopBits,
                                DataBits = DeviceSettings.DataBits,
                                NewLine = "\r\n",
                                ReadTimeout = 3000,
                                WriteTimeout = 3000
                            };

                            // Open the device
                            Log.Verbose("Opening device for communication.");
                            try
                            {
                                try
                                {
                                    if (!SerialPort.IsOpen)
                                    {
                                        RaiseDeviceInitializingEvent();
                                        SerialPort.Open();
                                        Log.Verbose("Device ready");
                                    }
                                    RaiseDeviceInitializedEvent();
                                }
                                catch (IOException ex)
                                {
                                    Log.Debug("Failed to open a connection with the device: {message}", ex.Message);
                                    CloseDevice(ex.Message);
                                    _timer.Change(ListenerInterval, Timeout.Infinite);
                                    continue;
                                }
                                catch (UnauthorizedAccessException ex)
                                {
                                    Log.Error(ex, "Unable to open a connection with the device");
                                    CloseDevice(ex.Message);
                                    _timer.Change(ListenerInterval, Timeout.Infinite);
                                    continue;
                                }
                                RaiseDeviceConnectedEvent();
                                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to connect to device.");
                                RaiseDeviceErrorEvent(ex);
                                CloseDevice(ex.Message);
                                _timer.Change(ListenerInterval, Timeout.Infinite);
                                continue;
                            }

                            return;
                        }
                }
                Log.Information("No pursuit alert device found. No connected devices match PID and VID.");
                RaiseDeviceDisconnectedEvent();
            }
            else
            {
                Log.Information("No pursuit alert device found. No com port names found matching PID and VID.");
                RaiseDeviceDisconnectedEvent();
            }
        }

        private void RaiseDeviceConnectedEvent()
        {
            DeviceIsConnected = true;
            _eventAggregator.GetEvent<DeviceConnectedEvent>().Publish(SerialPort);
        }

        private void RaiseDeviceDisconnectedEvent()
        {
            DeviceIsConnected = false;
            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();
        }

        private void RaiseDeviceErrorEvent(Exception ex)
        {
            if (!ListenerEventJitter)
            {
                _eventAggregator.GetEvent<DeviceErrorEvent>().Publish(ex);
                ListenerEventJitter = true;
                Task.Run(() =>
                {
                    Thread.Sleep(ListenerEventJitterTimeoutInMS * 4);
                    ListenerEventJitter = false;
                });
            }
        }

        private void RaiseDeviceFoundEvent(string serialPortName)
        {
            if (!ListenerEventJitter)
            {
                _eventAggregator.GetEvent<DeviceFoundEvent>().Publish(serialPortName);
                ListenerEventJitter = true;
                Task.Run(() =>
                {
                    Thread.Sleep(ListenerEventJitterTimeoutInMS);
                    ListenerEventJitter = false;
                });
            }
        }

        private void RaiseDeviceInitializedEvent() => _eventAggregator.GetEvent<DeviceInitializedEvent>().Publish();

        private void RaiseDeviceInitializingEvent()
        {
            if (!ListenerEventJitter)
            {
                _eventAggregator.GetEvent<DeviceInitializingEvent>().Publish();
                ListenerEventJitter = true;
                Task.Run(() =>
                {
                    Thread.Sleep(ListenerEventJitterTimeoutInMS);
                    ListenerEventJitter = false;
                });
            }
        }

        #endregion Methods
    }
}