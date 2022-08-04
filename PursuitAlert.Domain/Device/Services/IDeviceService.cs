using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PursuitAlert.Domain.Device.Services
{
    public interface IDeviceService
    {
        #region Properties

        string PortName { get; }

        #endregion Properties

        #region Methods

        void ForceDisconnect(string reason);

        void Listen();

        void LongFlashLED(int ledNumber, double timeout = 3);

        void Send(string data);

        void ShortFlashLED(int ledNumber, double timeout = 1);

        void StopListening(string reason);

        void TurnOffLED(int ledNumber);

        void TurnOnLED(int ledNumber);

        #endregion Methods
    }
}