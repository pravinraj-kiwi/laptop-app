using Prism.Events;
using PursuitAlert.Client.Services.Device.Errors;
using PursuitAlert.Client.Services.Device.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.LED
{
    public class LEDService : ILEDService
    {
        #region Fields

        private const string AllLEDsIdlePayload = "#LED,3,3,3,3,3,3,3";

        private const string AllLEDsOffPayload = "#LED,0,0,0,0,0,0,0";

        private const string AllLEDsOnPayload = "#LED,100,100,100,100,100,100,100";

        private readonly IEventAggregator _eventAggregator;

        private readonly object LEDStatusLock = new object();

        private Dictionary<int, int> CurrentLEDState;

        private Dictionary<int, Timer> FlashingLEDTasks = new Dictionary<int, Timer>();

        #endregion Fields

        #region Constructors

        public LEDService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            CurrentLEDState = new Dictionary<int, int>
            {
                [0] = (int)LEDState.Off,
                [1] = (int)LEDState.Off,
                [2] = (int)LEDState.Off,
                [3] = (int)LEDState.Off,
                [4] = (int)LEDState.Off,
                [5] = (int)LEDState.Off,
                [6] = (int)LEDState.Off,
            };
        }

        #endregion Constructors

        #region Methods

        public void BeginFlashing(SerialPort port, int ledNumber, double timeout = 1)
        {
            // Allen Brooks 5/27/2020: Update LEDs to flash when a mode is active TurnOnLED(modeChange.NewMode.ButtonPosition);
            var timerTask = new TimerCallback(state =>
            {
                // Set a half-second flash
                ShortFlashLED(port, ledNumber, 0.5);
            });
            FlashingLEDTasks.Add(ledNumber, new Timer(timerTask, null, 0, 1000));
        }

        public void SetAllLEDsIdle(SerialPort port)
        {
            Log.Debug("Turning off all LEDs on device connected to port {portName}", port.PortName);
            if (port == null)
            {
                Log.Warning("Device is null; cannot turn off all LEDs");
                return;
            }

            if (!port.IsOpen)
            {
                Log.Warning("Connection to device is closed; cannot turn off all LEDs");
                return;
            }

            WriteToDevice(AllLEDsIdlePayload, port);
        }

        public void SetInitialState(SerialPort port)
        {
            TurnOnAllLEDs(port);
            Task.Run(async () =>
            {
                await Task.Delay(500);
                SetAllLEDsIdle(port);
            });
        }

        public void ShortFlashLED(SerialPort port, int ledNumber, double timeout = 1)
        {
            Log.Verbose("Short flash LED {0} requested", ledNumber);
            var payload = BuildPayload(ledNumber, LEDState.On);
            WriteToDevice(payload, port);
            Task.Delay(TimeSpan.FromSeconds(timeout))
                .ContinueWith(_ =>
                {
                    payload = BuildPayload(ledNumber, LEDState.Off);
                    WriteToDevice(payload, port);
                });
        }

        public void StopFlashing(SerialPort port, int ledNumber)
        {
            FlashingLEDTasks[ledNumber].Change(Timeout.Infinite, Timeout.Infinite);
            FlashingLEDTasks[ledNumber].Dispose();
            FlashingLEDTasks.Remove(ledNumber);
        }

        public void TurnOffAllLEDs(SerialPort port)
        {
            Log.Debug("Turning off all LEDs on device connected to port {portName}", port.PortName);
            if (port == null)
            {
                Log.Warning("Device is null; cannot turn off all LEDs");
                return;
            }

            if (!port.IsOpen)
            {
                Log.Warning("Connection to device is closed; cannot turn off all LEDs");
                return;
            }

            WriteToDevice(AllLEDsOffPayload, port);
        }

        public void TurnOnAllLEDs(SerialPort port)
        {
            Log.Debug("Turning off all LEDs on device connected to port {portName}", port.PortName);
            if (port == null)
            {
                Log.Warning("Device is null; cannot turn off all LEDs");
                return;
            }

            if (!port.IsOpen)
            {
                Log.Warning("Connection to device is closed; cannot turn off all LEDs");
                return;
            }

            WriteToDevice(AllLEDsOnPayload, port);
        }

        private string BuildPayload(int ledNo, LEDState state)
        {
            var data = new StringBuilder();
            data.Append($"#LED,");

            // Allen Brooks 5/27/2020: If the LED state should be "off" set the state to the value
            // of the LED idle intensity
            var intensity = (int)state;

            lock (LEDStatusLock)
            {
                if (ledNo == 0)
                    CurrentLEDState[ledNo] = intensity;
                else
                    CurrentLEDState[ledNo - 1] = intensity;

                data.Append(string.Join(",", CurrentLEDState.Select(status => status.Value)));
            }
            var payload = data.ToString().TrimEnd(',');
            return payload;
        }

        private void WriteToDevice(string data, SerialPort device)
        {
            Log.Verbose("Sending payload to device: {payload}", data);
            data += "\r\n";
            try
            {
                device.Write(data);
            }
            catch (Exception ex)
            {
                // Wrap the error in a DeviceWriteException
                var error = new DeviceWriteException(ex);
                Log.Error(error, "Failed to write LED payload to device connected to port {portName}: {payload}", device.PortName, data);
                _eventAggregator.GetEvent<DeviceErrorEvent>().Publish(error);
            }
        }

        #endregion Methods
    }
}