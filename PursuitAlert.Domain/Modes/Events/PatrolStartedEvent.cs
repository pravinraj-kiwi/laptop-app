using Prism.Events;
using PursuitAlert.Domain.Modes.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Modes.Events
{
    /// <summary>
    /// The payload is the patrol mode configuration
    /// </summary>
    public class PatrolStartedEvent : PubSubEvent
    {
    }
}