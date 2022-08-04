using Prism.Events;
using PursuitAlert.Client.Properties;
using PursuitAlert.Client.Services.Device.Errors;
using PursuitAlert.Client.Services.Device.Events;
using PursuitAlert.Client.Services.GPS;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.Payloads
{
    public class PayloadService : IPayloadService
    {
        #region Fields

        private const int SwitchActivationJitterTimeout = 500;

        // Allen Brooks 5/27/2020: Updated after updated device firmware from Raptor with LED
        // intensity options
        private static readonly Regex GNGGAStatusIncompleteRegex = new Regex(@"\$GNGGA\,(?:\d{1,6}\.\d{1,2})?\,(?:\d{1,6}\.\d{1,6})?\,(?:[NS]?)?\,(?:\d{1,6}\.\d{1,6})?\,(?:[EW])?\,\d?\,\d{2}?\,(?:\d{1,2}\.\d{0,2})?\,(?:\d{1,6}\.\d{0,2})?\,M?\,(?:-?\d{1,6}\.\d{0,2})?\,M?\,.*\,(?:\d{0,6}\*\w{2})(?:\r)?\,\w{8}\,[\d\.]+");

        // Allen Brooks 5/27/2020: Updated after updated device firmware from Raptor with LED
        // intensity options
        private static readonly Regex GNGGAStatusRegex = new Regex(@"^\$GNGGA\,(?:[\d\.\-]+)?\,(?:[\d\.\-]+)?\,(?:[NS])?\,(?:[\d\.\-]+)?\,(?:[EW])?\,(?:[\d\.\-]+)?\,(?:[\d\.\-]+)?\,(?:[\d\.\-]+)?\,(?:[\d\.\-]+)?\,M?\,(?:[\d\.\-]+)?\,M?\,(?:\w*)?\,(?:[\w\*]*)?\,(?:\w{8})?\,(?:[\d\.\-]+)?$");

        // Allen Brooks 5/27/2020: Updated after updated device firmware from Raptor with LED
        // intensity options
        private static readonly Regex SWStatusIncompleteRegex = new Regex(@"\$SW\,[01]{1}\,[01]{1}\,[01]{1}\,[01]{1}\,[01]{1}\,?(?:\w{8})?(?:\r)?\,[\d\.]+");

        // Allen Brooks 5/27/2020: Updated after updated device firmware from Raptor with LED
        // intensity options
        private static readonly Regex SWStatusRegex = new Regex(@"^\$SW\,[01]{1}\,[01]{1}\,[01]{1}\,[01]{1}\,[01]{1}\,?(?:\w{8})?(?:\r)?\,[\d\.]+$");

        private static object PayloadProcessingLock = new object();

        private readonly IEventAggregator _eventAggregator;

        private readonly ICalculationService _gpsService;

        private bool _switchActivationJitter;

        private SerialDataReceivedEventHandler DataReceivedEventHandler;

        private SerialPort Device;

        private CoordinatesPayload LastCoordinates;

        private string SerialNumber;

        #endregion Fields

        #region Constructors

        public PayloadService(IEventAggregator eventAggregator,
            ICalculationService calculationService)
        {
            _eventAggregator = eventAggregator;
            _gpsService = calculationService;
        }

        #endregion Constructors

        #region Methods

        public string GetDeviceSerialNumber(SerialPort port)
        {
            SerialNumber = string.Empty;
            try
            {
                Log.Information("Waiting for device to emit serial number on port {portNumber}", port.PortName);
                ListenToDevice(port);
                while (string.IsNullOrEmpty(SerialNumber))
                {
                    Task.Delay(250).Wait();
                    Log.Information("Still waiting for device to emit serial number on port {portNumber}", port.PortName);
                }
                    
                Log.Debug("Serial number retrived: {serialNumber}", SerialNumber);
                return SerialNumber;
            }
            finally
            {
                StopListening();
            }
        }

        public void ListenToDevice(SerialPort port)
        {
            // Allen Brooks, 6/18/2020: Start listening with a clean slate, clear the buffer before
            // starting to listen
            Device = port;
            DataReceivedEventHandler = new SerialDataReceivedEventHandler(ProcessPayload);
            Device.DataReceived += DataReceivedEventHandler;
        }

        public async Task Process(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return;

            // Remove rogue '#' characters and control characters
            data = data.Replace("#", string.Empty);

            // "Clean" the payload by removing any control characters, e.g. new lines, carriage
            // returns, etc.
            data = new string(data.Where(c => !char.IsControl(c)).ToArray());

            // Check for a well-formed switch status payload
            if (IsWellFormedSwitchStatusPayload(data))
                HandleSwitchStatusPayload(data);

            // Check for a well-formed coordinate payload
            else if (IsWellFormedCoordinatesPayload(data))
                HandleCoordinatesPayload(data);

            // The payload wasn't recognized
            else
                Log.Warning("Unrecognized payload received: {0}", data);
        }

        public void StopListening()
        {
            Log.Verbose("Stopping listening to device");
            if (Device == null)
            {
                Log.Debug("No device is connected; cannot stop listening");
                return;
            }

            Device.DataReceived -= DataReceivedEventHandler;
            DataReceivedEventHandler = null;
            Device = null;
            Log.Debug("Stopped listening to device playloads");
        }

        private static bool IsWellFormedCoordinatesPayload(string payload) => GNGGAStatusRegex.IsMatch(payload);

        private static bool IsWellFormedSwitchStatusPayload(string payload) => SWStatusRegex.IsMatch(payload);

        private void DeactivateJitterIn(int switchActiveJitterTimeout)
        {
            Task.Run(() =>
            {
                Thread.Sleep(switchActiveJitterTimeout);
                _switchActivationJitter = false;
            })
                .ConfigureAwait(false);
        }

        private void HandleCoordinatesPayload(string payload)
        {
            Log.Verbose("Coordinate payload received (Well-formed): {0}", payload);
            var coordinatePayload = CoordinatesPayload.Parse(payload);
            if (coordinatePayload != null && LastCoordinates != null)
            {
                coordinatePayload.Speed = _gpsService.GetSpeed(coordinatePayload.ToCoordinates(), LastCoordinates.ToCoordinates());
                coordinatePayload.Bearing = _gpsService.GetBearing(coordinatePayload.ToCoordinates(), LastCoordinates.ToCoordinates());
            }

            // If the payload is null, keep moving
            if (coordinatePayload == null)
                return;

            LastCoordinates = coordinatePayload;

            if (!string.IsNullOrWhiteSpace(coordinatePayload.SerialNumber) && string.IsNullOrEmpty(SerialNumber))
            {
                Log.Verbose("Serial number found in coordinates payload: {serialNumber}", coordinatePayload.SerialNumber);
                SerialNumber = coordinatePayload.SerialNumber;
            }

            if (coordinatePayload.Longitude != 0 && !double.IsNaN(coordinatePayload.Longitude) && coordinatePayload.Latitude != 0 && !double.IsNaN(coordinatePayload.Latitude))
                _eventAggregator.GetEvent<GPSSignalAcquiredEvent>().Publish();
            else
                _eventAggregator.GetEvent<GPSSignalLostEvent>().Publish();

            _eventAggregator.GetEvent<CoordinatesReceivedEvent>().Publish(coordinatePayload);
        }

        private void HandleSwitchStatusPayload(string payload)
        {
            Log.Verbose("Switch status payload received (Well-formed): {0}", payload);
            var switchStatusPayload = SwitchStatusPayload.Parse(payload);
            _eventAggregator.GetEvent<SwitchStatusReceivedEvent>().Publish(switchStatusPayload);

            if (!string.IsNullOrWhiteSpace(switchStatusPayload.SerialNumber) && string.IsNullOrEmpty(SerialNumber))
            {
                Log.Verbose("Serial number found in switch status payload: {serialNumber}", switchStatusPayload.SerialNumber);
                SerialNumber = switchStatusPayload.SerialNumber;
            }

            if (switchStatusPayload.ActivatedButton != -1 && !_switchActivationJitter)
            {
                _switchActivationJitter = true;
                DeactivateJitterIn(SwitchActivationJitterTimeout);
                _eventAggregator.GetEvent<ButtonPressedEvent>().Publish(switchStatusPayload.ActivatedButton);
            }
        }

        private void ProcessPayload(object sender, SerialDataReceivedEventArgs e)
        {
            Log.Verbose("Processing Payload...");
            if (Device == null)
            {
                Log.Warning("Device is null or not connected; cannot process payload");
                return;
            }

            try
            {
                var rawPayload = Device.ReadLine();
                if (!string.IsNullOrWhiteSpace(rawPayload))
                {
                    // Separate the payload into lines in case multiple lines were received
                    var lines = rawPayload.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                        Process(line).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                var error = new DeviceReadException(ex);
                if (Device == null)
                    Log.Error(error, "Failed to process payload from device (device is disconnected)");
                else
                    Log.Error(error, "Failed to process payload from device connected to port {portName}", Device.PortName);

                _eventAggregator.GetEvent<DeviceErrorEvent>().Publish(error);
            }
        }

        #endregion Methods
    }
}