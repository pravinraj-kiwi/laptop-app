using Prism.Events;
using PursuitAlert.Domain.Configuration.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Configuration.Events
{
    public class DeviceConfigurationUpdatedEvent : PubSubEvent<DeviceConfigurationRetrievedEventArgs>
    {
    }
}