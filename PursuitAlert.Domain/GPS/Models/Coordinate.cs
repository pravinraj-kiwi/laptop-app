using PursuitAlert.Domain.Device.Payloads.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.GPS.Models
{
    public class Coordinate
    {
        #region Constructors

        /// <summary>
        /// A single GPS point
        /// </summary>
        /// <param name="longitude">
        /// The longitude
        /// </param>
        /// <param name="latitude">
        /// The latitude
        /// </param>
        public Coordinate(double longitude, double latitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        /// <summary>
        /// A single GPS point
        /// </summary>
        /// <param name="longitude">
        /// The longitude
        /// </param>
        /// <param name="latitude">
        /// The latitude
        /// </param>
        /// <param name="time">
        /// The time the location was recorded
        /// </param>
        public Coordinate(double longitude, double latitude, DateTime time)
        {
            Latitude = latitude;
            Longitude = longitude;
            Time = time;
        }

        /// <summary>
        /// Creates a new Coordinate from the given payload.
        /// </summary>
        /// <param name="payload">
        /// The coordinates received from the device.
        /// </param>
        public Coordinate(DeviceCoordinatesPayload payload)
        {
            Latitude = payload.Latitude;
            Longitude = payload.Longitude;
            var now = DateTime.UtcNow;
            Time = new DateTime(now.Year, now.Month, now.Day, payload.Time.Hours, payload.Time.Minutes, payload.Time.Seconds, payload.Time.Milliseconds);
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// The latitude
        /// </summary>
        public double Latitude { get; private set; }

        /// <summary>
        /// The longitude
        /// </summary>
        public double Longitude { get; private set; }

        /// <summary>
        /// The time the location was recorded
        /// </summary>
        public DateTime Time { get; private set; }

        #endregion Properties
    }
}