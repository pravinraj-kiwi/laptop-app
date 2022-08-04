using Prism.Events;
using PursuitAlert.Domain.Modes.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Modes.Events
{
    public class DelayedModeCanceledEvent : PubSubEvent<Mode>
    {
    }
}