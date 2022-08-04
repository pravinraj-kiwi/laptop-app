using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Events
{
    /// <summary>
    /// The payload is the port name
    /// </summary>
    public class DeviceFoundEvent : PubSubEvent<string>
    {
    }
}