using NmeaParser.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.Payloads
{
    public class CoordinatesPayload
    {
        #region Properties

        /// <summary>
        /// Altitude above mean sea level
        /// </summary>
        public double Altitude { get; set; }

        /// <summary>
        /// Altitude units: M (meters, fixed field)
        /// </summary>
        public string AltitudeUnits { get; set; }

        /// <summary>
        /// The bearing of the device since the last payload was received
        /// </summary>
        public double Bearing { get; set; }

        /// <summary>
        /// Age of differential corrections (null when DGPS is not used)
        /// </summary>
        public TimeSpan DifferentialAge { get; set; }

        /// <summary>
        /// ID of station providing differential corrections (null when DGPS is not used)
        /// </summary>
        public int DifferentialSatelliteStationId { get; set; }

        /// <summary>
        /// The latest firmware version loaded onto the device
        /// </summary>
        public float FirmwareVersion { get; set; }

        /// <summary>
        /// Geoid separation: difference between ellipsoid and mean sea level
        /// </summary>
        public double GeoidSeparation { get; set; }

        /// <summary>
        /// Geoid separation units: M (meters, fixed field)
        /// </summary>
        public string GeoidSeparationUnits { get; set; }

        /// <summary>
        /// Horizontal Dilution of Precision
        /// </summary>
        public double HDOP { get; set; }

        /// <summary>
        /// Checksum
        /// </summary>
        public byte HexadecimalChecksum { get; set; }

        /// <summary> Latitude (degrees & minutes) </summary>
        public double Latitude { get; set; }

        /// <summary> Longitude (degrees & minutes) </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// GGA Message Id
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// Quality indicator for position fix
        /// </summary>
        public Gga.FixQuality Quality { get; set; }

        /// <summary>
        /// Number of satellites used (range: 0-12)
        /// </summary>
        public int SatelliteCount { get; set; }

        /// <summary>
        /// The serial number of the connected device
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// The speed of the device since the last coordinate received
        /// </summary>
        public double Speed { get; set; }

        /// <summary>
        /// UTC time
        /// </summary>
        public TimeSpan Time { get; set; }

        #endregion Properties

        #region Methods

        public static CoordinatesPayload Parse(string data)
        {
            // Ex. $GNGGA,135726.00,3437.12933,N,08227.91770,W,1,08,1.55,262.6,M,-32.1,M,,*7E,fsy91itz,0.7
            Gga payload = null;

            // Allen Brooks 5/27/2020: Updated after updated device firmware from Raptor with LED
            // intensity options Strip the last 2 items from the payload (the last item is the
            // firmware version and the next-to-last is the serial number -- these values are not
            // valid GGA message format, so will cause an exception if we push them through the Nmea parser)
            var payloadPieces = data.Split(new char[] { ',' });

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

                data = string.Join(",", payloadItems);
            }

            try
            {
                payload = NmeaMessage.Parse(data) as Gga;
                var coordinatePayload = new CoordinatesPayload
                {
                    Altitude = payload.Altitude,
                    AltitudeUnits = payload.AltitudeUnits,
                    DifferentialAge = payload.TimeSinceLastDgpsUpdate.HasValue ? payload.TimeSinceLastDgpsUpdate.Value : default,
                    DifferentialSatelliteStationId = payload.DgpsStationId,
                    FirmwareVersion = firmwareVersion,
                    GeoidSeparation = payload.GeoidalSeparation,
                    GeoidSeparationUnits = payload.GeoidalSeparationUnits,
                    HDOP = payload.Hdop,
                    HexadecimalChecksum = payload.Checksum,
                    Latitude = payload.Latitude,
                    Longitude = payload.Longitude,
                    MessageId = payload.MessageType,
                    Quality = payload.Quality,
                    SatelliteCount = payload.NumberOfSatellites,
                    SerialNumber = serialNumber,
                    Time = payload.FixTime
                };
                return coordinatePayload;
            }
            catch
            {
                return null;
            }
        }

        #endregion Methods
    }
}