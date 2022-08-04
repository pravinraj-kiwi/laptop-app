using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Client.Infrastructure.API.Errors
{
    public class VehicleExistsException : Exception
    {
        #region Fields

        public const string MessageFormat = "Vehicle {0} already exists. Cannot create a new vehicle with unit ID {0}.";

        #endregion Fields

        #region Constructors

        public VehicleExistsException(string unitId)
            : base(string.Format(MessageFormat, unitId))
        {
        }

        #endregion Constructors
    }
}