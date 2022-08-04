using Newtonsoft.Json;
using Prism.Events;
using PursuitAlert.Domain.Application.Events;
using PursuitAlert.Domain.Device.Events;
using PursuitAlert.Domain.Device.Payloads.Services;
using PursuitAlert.Domain.Device.Services;
using PursuitAlert.Domain.Modes.Events;
using PursuitAlert.Domain.Modes.Models;
using PursuitAlert.Domain.Publishing.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PursuitAlert.Application.Device.Services
{
    public enum LEDState
    {
        Off = 3,

        On = 100
    }

    public class DeviceService : IDeviceService, IDisposable
    {
        #region Properties

        public int IdleLEDIntensity { get; private set; } = 3;

        public string PortName { get; }

        #endregion Properties

        #region Fields

        private static object LEDStatusLock = new object();

        private readonly IEventAggregator _eventAggregator;

        private readonly IDevicePayloadService _payloadService;

        private Dictionary<int, int> CurrentLEDStatus;

        private SerialDataReceivedEventHandler DataReceivedEventHandler;

        private SerialPort Device;

        private Dictionary<int, Timer> FlashingLEDTasks = new Dictionary<int, Timer>();

        #endregion Fields

        #region Constructors

        public DeviceService(IDevicePayloadService payloadService, IEventAggregator eventAggregator)
        {
            _payloadService = payloadService;
            _eventAggregator = eventAggregator;
            CurrentLEDStatus = new Dictionary<int, int>
            {
                [0] = IdleLEDIntensity,
                [1] = IdleLEDIntensity,
                [2] = IdleLEDIntensity,
                [3] = IdleLEDIntensity,
                [4] = IdleLEDIntensity,
                [5] = 0,
                [6] = 0
            };

            _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceConnected);
            _eventAggregator.GetEvent<ModeChangeEvent>().Subscribe(HandleModeChange);
            _eventAggregator.GetEvent<PinDroppedEvent>().Subscribe(HandlePinDrop);
            _eventAggregator.GetEvent<DelayedModeCountdownTimerTick>().Subscribe(HandleDelayedModeCountdown);
            _eventAggregator.GetEvent<SetDeviceSerialNumberEvent>().Subscribe(HandleSetDeviceSerialNumber);
            _eventAggregator.GetEvent<SetDeviceIdleLEDIntensityEvent>().Subscribe(SetIdleLEDIntensity);
            _eventAggregator.GetEvent<ApplicationExitEvent>().Subscribe(HandleApplicationExit);
        }

        #endregion Constructors

        #region Methods

        public void Dispose()
        {
            if (Device != null)
            {
                StopListening("Disposing DeviceService instance");
            }

            _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceConnected);
            _eventAggregator.GetEvent<ModeChangeEvent>().Unsubscribe(HandleModeChange);
            _eventAggregator.GetEvent<PinDroppedEvent>().Unsubscribe(HandlePinDrop);
            _eventAggregator.GetEvent<DelayedModeCountdownTimerTick>().Unsubscribe(HandleDelayedModeCountdown);
            _eventAggregator.GetEvent<SetDeviceSerialNumberEvent>().Unsubscribe(HandleSetDeviceSerialNumber);
            _eventAggregator.GetEvent<SetDeviceIdleLEDIntensityEvent>().Unsubscribe(SetIdleLEDIntensity);
            _eventAggregator.GetEvent<ApplicationExitEvent>().Unsubscribe(HandleApplicationExit);
        }

        public void ForceDisconnect(string reason)
        {
            Log.Information("Forcing device disconnect. Reason {0}", reason);
            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();
        }

        public void Listen()
        {
            // Since the device is just turning on, send the default LED state (all off)
            var payload = BuildLEDPayload(0, LEDState.Off);
            Send(payload);

            // Allen Brooks, 6/18/2020: Start listening with a clean slate, clear the buffer before
            // starting to listen
            DataReceivedEventHandler = new SerialDataReceivedEventHandler(ReadConstant);
            Device.DataReceived += DataReceivedEventHandler;
        }

        public void LongFlashLED(int ledNumber, double timeout = 3)
        {
            Log.Verbose("Long flash LED {0} requested", ledNumber);
            var payload = BuildLEDPayload(ledNumber, LEDState.On);
            Send(payload);
            Task.Delay(TimeSpan.FromSeconds(timeout))
                .ContinueWith(_ =>
                {
                    payload = BuildLEDPayload(ledNumber, LEDState.Off);
                    Send(payload);
                });
        }

        public void Send(string data)
        {
            Log.Verbose("Sending payload to device: {0}", data);
            data += "\r\n";
            try
            {
                Device.Write(data);
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning(ex, "Failed to send payload to device. Device appears to have been disconnected: {payload}", data);
                Device.Close();
                Device.Dispose();
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();
            }
        }

        public void SetIdleLEDIntensity(int intensity = 2)
        {
            IdleLEDIntensity = 2;
            CurrentLEDStatus = new Dictionary<int, int>
            {
                [0] = IdleLEDIntensity,
                [1] = IdleLEDIntensity,
                [2] = IdleLEDIntensity,
                [3] = IdleLEDIntensity,
                [4] = IdleLEDIntensity,
                [5] = 0,
                [6] = 0
            };
        }

        public void ShortFlashLED(int ledNumber, double timeout = 1)
        {
            Log.Verbose("Short flash LED {0} requested", ledNumber);
            var payload = BuildLEDPayload(ledNumber, LEDState.On);
            Send(payload);
            Task.Delay(TimeSpan.FromSeconds(timeout))
                .ContinueWith(_ =>
                {
                    payload = BuildLEDPayload(ledNumber, LEDState.Off);
                    Send(payload);
                });
        }

        public void StopListening(string reason)
        {
            Log.Verbose("Stop listening to device (reason: {0})", reason);
            if (DataReceivedEventHandler != null)
                Device.DataReceived -= DataReceivedEventHandler;
            DataReceivedEventHandler = null;
        }

        public void TurnAllLEDsOff()
        {
            if (Device == null)
            {
                Log.Debug("Device is disconnected. Cannot turn all LEDs off.");
                return;
            }

            if (!Device.IsOpen)
            {
                Log.Debug("Device port is closed. Cannot turn all LEDs off.");
                return;
            }

            Log.Verbose("Turning off all LEDs");
            Send("#LED,0,0,0,0,0,0,0");
        }

        public void TurnOffLED(int ledNumber)
        {
            Log.Verbose("Turn off LED {0} requested", ledNumber);
            var payload = BuildLEDPayload(ledNumber, LEDState.Off);
            Send(payload);
        }

        public void TurnOnLED(int ledNumber)
        {
            Log.Verbose("Turn on LED {0} requested", ledNumber);
            var payload = BuildLEDPayload(ledNumber, LEDState.On);
            Send(payload);
        }

        private string BuildLEDPayload(int ledNo, LEDState state)
        {
            var data = new StringBuilder();
            data.Append($"#LED,");

            // Allen Brooks 5/27/2020: If the LED state should be "off" set the state to the value
            // of the LED idle intensity
            var intensity = (int)state;
            if (state == LEDState.Off)
                intensity = IdleLEDIntensity;

            lock (LEDStatusLock)
            {
                if (ledNo == 0)
                    CurrentLEDStatus[ledNo] = intensity;
                else
                    CurrentLEDStatus[ledNo - 1] = intensity;

                data.Append(string.Join(",", CurrentLEDStatus.Select(status => status.Value)));
            }
            var payload = data.ToString().TrimEnd(',');
            return payload;
        }

        private void HandleApplicationExit(int exitCode)
        {
            TurnAllLEDsOff();

            StopListening($"Application exit (exit code: {exitCode})");

            if (Device != null)
            {
                if (Device.IsOpen)
                    Device.Close();
                Device.Dispose();
            }
        }

        private void HandleDelayedModeCountdown(Mode mode) => ShortFlashLED(mode.ButtonPosition, 0.25);

        private void HandleDeviceConnected(SerialPort device)
        {
            Device = device;
            Listen();
            if (!_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleDeviceDisconnected))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleDeviceDisconnected);
            if (_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceConnected))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Unsubscribe(HandleDeviceConnected);
        }

        private void HandleDeviceDisconnected()
        {
            foreach (var task in FlashingLEDTasks)
            {
                task.Value.Change(Timeout.Infinite, Timeout.Infinite);
                task.Value.Dispose();
            }
            FlashingLEDTasks.Clear();
            if (_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleDeviceDisconnected))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(HandleDeviceDisconnected);

            if (!_eventAggregator.GetEvent<DeviceConnectedEvent>().Contains(HandleDeviceConnected))
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(HandleDeviceConnected);
        }

        private void HandleModeChange(ModeChangeEventArgs modeChange)
        {
            if (modeChange.ChangeType == ModeChangeType.ModeEngaged)
            {
                // Allen Brooks 5/27/2020: Update LEDs to flash when a mode is active TurnOnLED(modeChange.NewMode.ButtonPosition);

                var timerTask = new TimerCallback(state =>
                {
                    // Set a half-second flash
                    ShortFlashLED(modeChange.NewMode.ButtonPosition, 0.5);
                });
                FlashingLEDTasks.Add(modeChange.NewMode.ButtonPosition, new Timer(timerTask, null, 0, 1000));
            }
            else if (modeChange.ChangeType == ModeChangeType.ModeDisengaged)
            {
                // TurnOffLED(modeChange.OriginalMode.ButtonPosition);
                FlashingLEDTasks[modeChange.OriginalMode.ButtonPosition].Change(Timeout.Infinite, Timeout.Infinite);
                FlashingLEDTasks[modeChange.OriginalMode.ButtonPosition].Dispose();
                FlashingLEDTasks.Remove(modeChange.OriginalMode.ButtonPosition);
            }
        }

        private void HandlePinDrop(Mode mode) => ShortFlashLED(mode.ButtonPosition);

        private void HandleSetDeviceSerialNumber(string serialNumber)
        {
            try
            {
                Log.Information("Setting device serial number to {0}", serialNumber);
                var payload = $"#ESN,{serialNumber}\r\n";
                Send(payload);
                _eventAggregator.GetEvent<DeviceSerialNumberSetEvent>().Publish(serialNumber);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Couldn't set serial number");
                _eventAggregator.GetEvent<SetDeviceSerialNumberFailedEvent>().Publish(ex);
            }
        }

        private void ReadConstant(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var rawPayload = Device.ReadLine();
                _eventAggregator.GetEvent<DeviceConnectedEvent>().Publish(Device);
                if (!string.IsNullOrWhiteSpace(rawPayload))
                {
                    // Separate the payload into lines in case multiple lines were received
                    var lines = rawPayload.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                        _payloadService.Process(line).ConfigureAwait(false);
                }
            }
            catch (TimeoutException ex)
            {
                Log.Error(ex, "Read operation timed out");
                Device.Close();
                Device.Dispose();
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();
            }
            catch (IOException ex)
            {
                Log.Error(ex, "Reading device failed");
                Device.Close();
                Device.Dispose();
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Error(ex, "Read operation not allowed");
                Device.Close();
                Device.Dispose();
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();
            }
        }

        #endregion Methods
    }
}