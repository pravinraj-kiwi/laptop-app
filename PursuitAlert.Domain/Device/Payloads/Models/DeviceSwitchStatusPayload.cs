using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Payloads.Models
{
    public class DeviceSwitchStatusPayload
    {
        #region Properties

        public int ActivatedButton { get; set; }

        public Dictionary<int, bool> ButtonStatuses { get; set; }

        public float FirmwareVersion { get; set; }

        public string SerialNumber { get; set; }

        #endregion Properties
    }
}