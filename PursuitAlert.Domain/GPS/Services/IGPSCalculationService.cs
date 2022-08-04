using PursuitAlert.Domain.Device.Payloads.Models;
using PursuitAlert.Domain.GPS.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.GPS.Services
{
    public interface IGPSCalculationService
    {
        #region Methods

        double DistanceBetween(DeviceCoordinatesPayload startingPoint, DeviceCoordinatesPayload endingPoint);

        double GetBearing(DeviceCoordinatesPayload coordinates, DeviceCoordinatesPayload previousCoordinates);

        double GetSpeed(DeviceCoordinatesPayload coordinates, DeviceCoordinatesPayload previousCoordinates);

        #endregion Methods
    }
}