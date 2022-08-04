using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device
{
    public interface IDeviceService
    {
        #region Properties

        SerialPort Device { get; }

        bool IsDeviceConnected { get; }

        string SerialNumber { get; }
        bool IsDeviceInitialized { get; }

        void CloseConnection(string reason = "");

        #endregion Properties

        #region Methods

        void Initialize();

        void StopListening();

        #endregion Methods
    }
}