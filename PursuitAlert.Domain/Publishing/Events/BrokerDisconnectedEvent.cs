﻿using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Publishing.Events
{
    public class BrokerDisconnectedEvent : PubSubEvent<string>
    {
    }
}