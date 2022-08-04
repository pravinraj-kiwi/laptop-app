using Prism.Events;
using PursuitAlert.Client.Services.Modes.Events.EventPayloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Modes.Events
{
    public class ModeChangeEvent : PubSubEvent<ModeChangeEventPayload>
    {
    }
}