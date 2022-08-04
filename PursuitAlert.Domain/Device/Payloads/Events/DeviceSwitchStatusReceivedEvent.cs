using Prism.Events;
using PursuitAlert.Domain.Device.Payloads.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Payloads.Events
{
    public class DeviceSwitchStatusReceivedEvent : PubSubEvent<DeviceSwitchStatusPayload>
    {
    }
}