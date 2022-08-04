using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Events
{
    public class SetDeviceIdleLEDIntensityEvent : PubSubEvent<int>
    {
    }
}