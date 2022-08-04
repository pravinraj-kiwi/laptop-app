using NmeaParser.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Payloads.Models
{
    public class DeviceCoordinatesPayload
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
        /// UTC time
        /// </summary>
        public TimeSpan Time { get; set; }
        public double Bearing { get; set; }
        public double Speed { get; set; }

        #endregion Properties
    }
}