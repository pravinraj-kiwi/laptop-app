using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Events
{
    public class DeviceErrorEvent : PubSubEvent<Exception>
    {
    }
}