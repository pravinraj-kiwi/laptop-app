using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.Payloads
{
    public interface IPayloadService
    {
        #region Methods

        string GetDeviceSerialNumber(SerialPort port);

        void ListenToDevice(SerialPort port);

        void StopListening();

        #endregion Methods
    }
}