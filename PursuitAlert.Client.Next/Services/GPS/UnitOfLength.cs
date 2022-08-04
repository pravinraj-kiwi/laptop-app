using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.GPS
{
    /// <summary>
    /// Used to convert values between meters, kilometers, and miles.
    /// </summary>
    public class UnitOfLength
    {
        #region Fields

        public static UnitOfLength Kilometers = new UnitOfLength(1.609344);

        public static UnitOfLength Miles = new UnitOfLength(1);

        public static UnitOfLength NauticalMiles = new UnitOfLength(0.8684);

        private readonly double _fromMilesFactor;

        #endregion Fields

        #region Constructors

        private UnitOfLength(double fromMilesFactor)
        {
            _fromMilesFactor = fromMilesFactor;
        }

        #endregion Constructors

        #region Methods

        public double ConvertFromMiles(double input)
        {
            return input * _fromMilesFactor;
        }

        #endregion Methods
    }
}