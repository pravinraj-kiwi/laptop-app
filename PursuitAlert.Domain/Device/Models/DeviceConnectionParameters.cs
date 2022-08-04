using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Models
{
    public class DeviceConnectionParameters
    {
        #region Properties

        public int BaudRate { get; set; }

        public int ButtonCount { get; set; }

        public int DataBits { get; set; }

        public int Handshake { get; set; }

        public int Parity { get; set; }

        public string PID { get; set; }

        public int ReadTimeout { get; set; }

        public int StopBits { get; set; }

        public int SwitchActivationJitterInMS { get; set; }

        public string VID { get; set; }

        public int WriteTimeout { get; set; }

        #endregion Properties
    }
}