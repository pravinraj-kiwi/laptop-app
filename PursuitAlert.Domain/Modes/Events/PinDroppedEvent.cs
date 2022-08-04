using Prism.Events;
using PursuitAlert.Domain.Modes.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Modes.Events
{
    /// <summary>
    /// The payload is the mode configuration for the pin drop
    /// </summary>
    public class PinDroppedEvent : PubSubEvent<Mode>
    {
    }
}