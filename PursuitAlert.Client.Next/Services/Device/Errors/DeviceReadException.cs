using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.Errors
{
    public class DeviceReadException : Exception
    {
        #region Fields

        private const string MessageFormat = "A failure occurred while attempting to get data from the device.";

        #endregion Fields

        #region Constructors

        public DeviceReadException()
            : base(MessageFormat)
        {
        }

        public DeviceReadException(Exception innerException)
            : base(MessageFormat, innerException)
        {
        }

        #endregion Constructors
    }
}