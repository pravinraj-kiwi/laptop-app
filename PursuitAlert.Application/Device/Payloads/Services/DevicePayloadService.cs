using NmeaParser.Messages;
using Prism.Events;
using PursuitAlert.Domain.Device.Events;
using PursuitAlert.Domain.Device.Payloads.Events;
using PursuitAlert.Domain.Device.Payloads.Models;
using PursuitAlert.Domain.Device.Payloads.Services;
using PursuitAlert.Domain.GPS.Models;
using PursuitAlert.Domain.GPS.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PursuitAlert.Application.Device.Payloads.Services
{
    public class DevicePayloadService : IDevicePayloadService
    {
        #region Fields

        private const string SwitchStatusPreamble = "$SW,";

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

        private readonly IEventAggregator _eventAggregator;

        private readonly IGPSCalculationService _gpsService;

        private object _lockObject = new object();

        private bool FirstGPSSignalReceived = true;

        private DeviceCoordinatesPayload LastPayload;

        private bool SwitchActivationJitter;

        private int SwitchActivationJitterTimeout = 500;

        #endregion Fields

        #region Constructors

        public DevicePayloadService(IEventAggregator eventAggregator,
            IGPSCalculationService gpsService)
        {
            _eventAggregator = eventAggregator;
            _gpsService = gpsService;
            _eventAggregator.GetEvent<DeviceConnectedEvent>().Subscribe(device => FirstGPSSignalReceived = true);
        }

        #endregion Constructors

        #region Methods

        public async Task Process(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return;

            // Uncomment this line if you want to see the message as it came from the device
            // Log.Debug("Processing payload: {0}", payload);

            // Remove rogue '#' characters and control characters
            payload = payload
                .Replace("#", string.Empty);

            payload = new string(payload.Where(c => !char.IsControl(c)).ToArray());

            lock (_lockObject)
            {
                // Check for a well-formed switch status payload
                if (IsWellFormedSwitchStatusPayload(payload))
                    HandleSwitchStatusPayload(payload);

                // Check for a well-formed coordinate payload
                else if (IsWellFormedCoordinatesPayload(payload))
                    HandleCoordinatesPayload(payload);

                // The payload wasn't recognized
                else
                    Log.Warning("Unrecognized payload received: {0}", payload);
            }
        }

        public void SetSwitchActivationJitter(int jitter) => SwitchActivationJitterTimeout = jitter;

        private void DeactivateJitterIn(int switchActiveJitterTimeout)
        {
            Task.Run(() =>
            {
                Thread.Sleep(switchActiveJitterTimeout);
                SwitchActivationJitter = false;
            })
                .ConfigureAwait(false);
        }

        private void HandleCoordinatesPayload(string payload)
        {
            Log.Verbose("Coordinate payload received (Well-formed): {0}", payload);
            var coordinatePayload = ParseCoordinatesPayload(payload);

            // If the payload is null, keep moving
            if (coordinatePayload == null)
                return;

            if (!string.IsNullOrWhiteSpace(coordinatePayload.SerialNumber))
            {
                Log.Verbose("Serial number found in payload. Dispatching serial number as event");
                _eventAggregator.GetEvent<DeviceSerialNumberCapturedEvent>().Publish(coordinatePayload.SerialNumber);
            }
            else
            {
                Log.Debug("No serial number found in payload. Dispatching the serial number not found event.");
                _eventAggregator.GetEvent<DeviceSerialNumberNotFoundEvent>().Publish();
            }

            _eventAggregator.GetEvent<CoordinatesPayloadReceivedEvent>().Publish(coordinatePayload);
        }

        private void HandleSwitchStatusPayload(string payload)
        {
            Log.Verbose("Switch status payload received (Well-formed): {0}", payload);
            var switchStatusPayload = ParseSwitchStatus(payload);
            _eventAggregator.GetEvent<DeviceSwitchStatusReceivedEvent>().Publish(switchStatusPayload);

            if (switchStatusPayload.ActivatedButton != -1 && !SwitchActivationJitter)
            {
                SwitchActivationJitter = true;
                DeactivateJitterIn(SwitchActivationJitterTimeout);

                _eventAggregator.GetEvent<DeviceSwitchActivatedEvent>().Publish(switchStatusPayload.ActivatedButton);
            }
        }

        private bool IsWellFormedCoordinatesPayload(string payload) => GNGGAStatusRegex.IsMatch(payload);

        private bool IsWellFormedSwitchStatusPayload(string payload) => SWStatusRegex.IsMatch(payload);

        private DeviceCoordinatesPayload ParseCoordinatesPayload(string payload)
        {
            // Ex. $GNGGA,135726.00,3437.12933,N,08227.91770,W,1,08,1.55,262.6,M,-32.1,M,,*7E,fsy91itz,0.7
            Gga data = null;

            // Allen Brooks 5/27/2020: Updated after updated device firmware from Raptor with LED
            // intensity options Strip the last 2 items from the payload (the last item is the
            // firmware version and the next-to-last is the serial number -- these values are not
            // valid GGA message format, so will cause an exception if we push them through the Nmea parser)
            var payloadPieces = payload.Split(new char[] { ',' });

            // Make sure the last item is a float, it will be the firmware version. If it's not we
            // have an invalid message anyway
            var serialNumber = string.Empty;
            var firmwareVersion = 0.0f;
            if (float.TryParse(payloadPieces.Last(), out float _))
            {
                var payloadItems = new List<string>(payloadPieces);

                var firmwareIndex = payloadItems.Count - 1;
                var serialNumberIndex = payloadItems.Count - 2;

                serialNumber = payloadItems[serialNumberIndex];
                firmwareVersion = float.TryParse(payloadItems[firmwareIndex], out float firmwareAttempt) ? firmwareAttempt : 0.0f;

                payloadItems.RemoveAt(firmwareIndex);
                payloadItems.RemoveAt(serialNumberIndex);

                payload = string.Join(",", payloadItems);
            }

            try
            {
                // TODO: Calculate the speed and bearing here
                data = NmeaMessage.Parse(payload) as Gga;
                var coordinatePayload = new DeviceCoordinatesPayload
                {
                    Altitude = data.Altitude,
                    AltitudeUnits = data.AltitudeUnits,
                    DifferentialAge = data.TimeSinceLastDgpsUpdate.HasValue ? data.TimeSinceLastDgpsUpdate.Value : default,
                    DifferentialSatelliteStationId = data.DgpsStationId,
                    FirmwareVersion = firmwareVersion,
                    GeoidSeparation = data.GeoidalSeparation,
                    GeoidSeparationUnits = data.GeoidalSeparationUnits,
                    HDOP = data.Hdop,
                    HexadecimalChecksum = data.Checksum,
                    Latitude = data.Latitude,
                    Longitude = data.Longitude,
                    MessageId = data.MessageType,
                    Quality = data.Quality,
                    SatelliteCount = data.NumberOfSatellites,
                    SerialNumber = serialNumber,
                    Time = data.FixTime
                };
                coordinatePayload.Speed = _gpsService.GetSpeed(coordinatePayload, LastPayload);
                coordinatePayload.Bearing = _gpsService.GetBearing(coordinatePayload, LastPayload);
                LastPayload = coordinatePayload;
                return coordinatePayload;
            }
            catch
            {
                return null;
            }
        }

        private DeviceSwitchStatusPayload ParseSwitchStatus(string payload)
        {
            // Ex. $SW,0,0,0,0,0,fsy91itz,0.7
            var result = new DeviceSwitchStatusPayload();

            // Replace the preamble
            payload = payload.Replace(SwitchStatusPreamble, string.Empty);

            // Split into segments
            var payloadPieces = payload.Split(new char[] { ',' });

            // Allen Brooks 5/27/2020: Updated after updated device firmware from Raptor with LED
            // intensity options Get the firmware version (always the last item)
            result.FirmwareVersion = float.TryParse(payloadPieces.Last(), out float firmwareVersion) ? firmwareVersion : 0.0f;

            // Allen Brooks 5/27/2020: Updated after updated device firmware from Raptor with LED
            // intensity options Get the serial number (always the next-to-last item)
            result.SerialNumber = payloadPieces[payloadPieces.Length - 2].ToString();

            // Get the activated button
            var activatedButton = -1;
            if (payloadPieces.Any(status => status == "1"))
                activatedButton = Array.IndexOf(payloadPieces, payloadPieces.First(status => status == "1")) + 1;

            // Remove the serial number from the payload to get the button statuses, convert the
            // values to a dictionary of { buttonNumber, isActivated }
            var buttonStatuses =
                payloadPieces.Take(payloadPieces.Length - 1)
                .Select((value, index) => new { value, index })

                // Status is active if the value isn't empty and if it equals 1
                .ToDictionary(status => status.index + 1, status => !string.IsNullOrWhiteSpace(status.value) && status.value == "1");

            result.ActivatedButton = activatedButton;
            result.ButtonStatuses = buttonStatuses;

            return result;
        }

        #endregion Methods
    }
}