using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Events
{
    /// <summary>
    /// The payload is the switch number
    /// </summary>
    public class UnmappedSwitchActivatedEvent : PubSubEvent<int>
    {
    }
}