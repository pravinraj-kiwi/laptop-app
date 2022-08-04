using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.Connection
{
    public interface IDeviceConnectionMonitorService
    {
        #region Properties

        SerialPort ConnectedDevice { get; }

        #endregion Properties

        #region Methods

        void Disconnect(string reason = "");
        void StopWatchingForDeviceConnection();
        void StopWatchingForDeviceDisconnection();
        bool TryConnect(out SerialPort connectedDevice);

        void WatchForDeviceConnection();

        void WatchForDeviceDisconnection();

        #endregion Methods
    }
}