using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.GPS
{
    public class Coordinates
    {
        #region Properties

        public double Bearing { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double Speed { get; set; }

        public DateTime Time { get; set; }

        #endregion Properties

        #region Methods

        public bool HasLocationData()
        {
            return !double.IsNaN(Latitude) && !double.IsNaN(Longitude) && Latitude != 0.00 && Longitude != 0.00;
        }

        #endregion Methods
    }
}