using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Payloads.Events
{
    /// <summary>
    /// The payload is the number of the switch that was activated
    /// </summary>
    public class DeviceSwitchActivatedEvent : PubSubEvent<int>
    {
    }
}