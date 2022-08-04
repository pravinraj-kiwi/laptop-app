using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Application.Events
{
    public class ApplicationExitEvent : PubSubEvent<int>
    {
    }
}
