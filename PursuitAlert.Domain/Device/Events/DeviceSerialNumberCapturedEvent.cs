using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Events
{
    /// <summary>
    /// The payload is the serial number
    /// </summary>
    public class DeviceSerialNumberCapturedEvent : PubSubEvent<string>
    {
    }
}