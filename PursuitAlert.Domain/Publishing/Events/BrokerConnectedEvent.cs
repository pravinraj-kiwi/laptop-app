using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Publishing.Events
{
    /// <summary>
    /// The payload should be the server's URL.
    /// </summary>
    public class BrokerConnectedEvent : PubSubEvent<string>
    {
    }
}