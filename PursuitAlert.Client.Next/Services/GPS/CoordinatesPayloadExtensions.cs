using PursuitAlert.Client.Services.Device.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.GPS
{
    public static class CoordinatesPayloadExtensions
    {
        #region Methods

        public static Coordinates ToCoordinates(this CoordinatesPayload payload)
        {
            if (payload == null)
                return default;

            var payloadTime = DateTimeOffset.FromUnixTimeSeconds((long)payload.Time.TotalSeconds).LocalDateTime;
            var payloadDateTime = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, payloadTime.Hour, payloadTime.Minute, payloadTime.Second, payloadTime.Millisecond);

            var coordinates = new Coordinates
            {
                Bearing = payload.Bearing,
                Latitude = payload.Latitude,
                Longitude = payload.Longitude,
                Speed = payload.Speed,
                Time = payloadDateTime
            };

            return coordinates;
        }

        #endregion Methods
    }
}