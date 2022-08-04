using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.LED
{
    public interface ILEDService
    {
        void BeginFlashing(SerialPort port, int ledNumber, double timeout = 1);
        #region Methods

        void SetInitialState(SerialPort port);
        void ShortFlashLED(SerialPort port, int ledNumber, double timeout = 1);
        void StopFlashing(SerialPort port, int ledNumber);
        void TurnOffAllLEDs(SerialPort port);

        #endregion Methods
    }
}