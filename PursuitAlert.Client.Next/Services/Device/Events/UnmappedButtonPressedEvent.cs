using Prism.Events;
using PursuitAlert.Client.Services.Device.Events.EventPayloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.Events
{
    public class UnmappedButtonPressedEvent : PubSubEvent<UnmappedButtonPressedEventPayload>
    {
    }
}