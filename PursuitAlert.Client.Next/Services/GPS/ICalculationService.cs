using PursuitAlert.Client.Services.Device.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.GPS
{
    public interface ICalculationService
    {
        #region Methods

        string BearingToCardinal(double bearingInRadians);

        double DistanceBetween(Coordinates coordinates, Coordinates previousCoordinates);

        double GetBearing(Coordinates coordinates, Coordinates previousCoordinates);

        double GetSpeed(Coordinates coordinates, Coordinates previousCoordinates);

        #endregion Methods
    }
}