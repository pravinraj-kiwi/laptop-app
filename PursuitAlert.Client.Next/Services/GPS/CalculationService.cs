using PursuitAlert.Client.Services.Device.Payloads;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.GPS
{
    public class CalculationService : ICalculationService
    {
        #region Methods

        public string BearingToCardinal(double bearingInDegrees)
        {
            string[] cardinals = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N" };
            return cardinals[(int)Math.Round(((double)bearingInDegrees * 10 % 3600) / 225)];
        }

        public double DistanceBetween(Coordinates coordinates, Coordinates previousCoordinates)
        {
            var distanceBetween = 0.00;
            var unitOfLength = UnitOfLength.Miles;

            if (coordinates.Latitude == default)
                throw new ArgumentNullException(nameof(coordinates.Latitude));

            if (coordinates.Longitude == default)
                throw new ArgumentNullException(nameof(coordinates.Longitude));

            if (previousCoordinates == null)
                return distanceBetween;

            if (previousCoordinates.Latitude == default)
                throw new ArgumentOutOfRangeException(nameof(previousCoordinates.Latitude));

            if (previousCoordinates.Longitude == default)
                throw new ArgumentOutOfRangeException(nameof(previousCoordinates.Longitude));

            var baseRad = Math.PI * coordinates.Latitude / 180;
            var targetRad = Math.PI * previousCoordinates.Latitude / 180;
            var theta = coordinates.Longitude - previousCoordinates.Longitude;
            var thetaRad = Math.PI * theta / 180;

            double dist =
                Math.Sin(baseRad) * Math.Sin(targetRad) + Math.Cos(baseRad) *
                Math.Cos(targetRad) * Math.Cos(thetaRad);
            dist = Math.Acos(dist);

            dist = dist * 180 / Math.PI;
            dist = dist * 60 * 1.1515;

            return unitOfLength.ConvertFromMiles(dist);
        }

        public double GetBearing(Coordinates coordinates, Coordinates previousCoordinates)
        {
            var bearing = 0.00;
            try
            {
                if (coordinates.Latitude == default)
                    throw new ArgumentNullException(nameof(coordinates.Latitude));

                if (coordinates.Longitude == default)
                    throw new ArgumentNullException(nameof(coordinates.Longitude));

                if (double.IsNaN(coordinates.Latitude))
                    return bearing;

                if (double.IsNaN(coordinates.Longitude))
                    return bearing;

                if (previousCoordinates == null)
                    return bearing;

                if (previousCoordinates.Latitude == default || double.IsNaN(previousCoordinates.Latitude))
                    throw new ArgumentOutOfRangeException(nameof(previousCoordinates.Latitude));

                if (previousCoordinates.Longitude == default || double.IsNaN(previousCoordinates.Longitude))
                    throw new ArgumentOutOfRangeException(nameof(previousCoordinates.Longitude));

                var deltaLongitude = DegreesToRadians(coordinates.Longitude - previousCoordinates.Longitude);
                var deltaPhi = Math.Log(Math.Tan(DegreesToRadians(coordinates.Latitude) / 2 + Math.PI / 4) / Math.Tan(DegreesToRadians(previousCoordinates.Latitude) / 2 + Math.PI / 4));
                if (Math.Abs(deltaLongitude) > Math.PI)
                    deltaLongitude = deltaLongitude > 0 ? -(2 * Math.PI - deltaLongitude) : (2 * Math.PI + deltaLongitude);
                return ToBearing(Math.Atan2(deltaLongitude, deltaPhi));

                //double x = Math.Cos(DegreesToRadians(coordinates.Latitude)) * Math.Sin(DegreesToRadians(previousCoordinates.Latitude)) - Math.Sin(DegreesToRadians(coordinates.Latitude)) * Math.Cos(DegreesToRadians(previousCoordinates.Latitude)) * Math.Cos(DegreesToRadians(previousCoordinates.Longitude - coordinates.Longitude));
                //double y = Math.Sin(DegreesToRadians(previousCoordinates.Longitude - coordinates.Longitude)) * Math.Cos(DegreesToRadians(previousCoordinates.Latitude));

                // Math.Atan2 can return negative value, 0 <= output value < 2*PI expected
                //bearing = (Math.Atan2(y, x) + Math.PI * 2) % (Math.PI * 2);
            }
            catch (Exception ex)
            {
                Log.Debug("Bearing calculation has failed: {0}", ex.Message);
            }

            return Convert.ToInt32(bearing);
        }

        /// <summary>
        /// Calculates the speed at which a vehicle has traveled to get from a starting point to a checkpoint
        /// </summary>
        /// <param name="startingPoint">The starting point</param>
        /// <param name="checkpoint">The next point</param>
        /// <returns></returns>
        public double GetSpeed(Coordinates coordinates, Coordinates previousCoordinates)
        {
            var speed = 0.00;
            try
            {
                if (coordinates == null)
                    throw new ArgumentNullException(nameof(coordinates));

                // If there are no previous coordinates, return a 0 speed
                if (previousCoordinates == null)
                    return speed;

                // Get the distance between the 2 points
                var distance = DistanceBetween(coordinates, previousCoordinates);

                // Calculate the time taken to get from the starting point to the
                // previousCoordinates in hours
                var travelTime = coordinates.Time.Subtract(previousCoordinates.Time).TotalHours;

                // Calculate the speed
                speed = Math.Abs(distance / travelTime);
            }
            catch (Exception ex)
            {
                Log.Debug("Speed calculation has failed: {0}", ex.Message);
            }

            // Do not return negative speeds (if the vehicle is stationary)
            if (speed < 0 || double.IsNaN(speed))
                return 0.0;

            return speed;
        }

        public double ToBearing(double radians)
        {
            // Convert radians to degrees (as bearing: 0...360)
            return (RadiansToDegress(radians) + 360) % 360;
        }

        /// <summary>
        /// Utility method to convert degrees to radians
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        private double DegreesToRadians(double angle)
        {
            return angle * Math.PI / 180.0d;
        }

        /// <summary>
        /// Utility method to convert radians to degrees
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        private double RadiansToDegress(double radians)
        {
            return radians * (180d / Math.PI);
        }

        #endregion Methods
    }
}