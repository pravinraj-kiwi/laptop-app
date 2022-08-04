using Prism.Events;
using PursuitAlert.Domain.Device.Services;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace PursuitAlert.Domain.Device.Events
{
    /// <summary>
    /// Payload is the port name
    /// </summary>
    public class DeviceConnectedEvent : PubSubEvent<SerialPort>
    {
    }
}