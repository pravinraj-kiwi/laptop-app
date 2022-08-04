using Prism.Events;
using PursuitAlert.Domain.Device.Payloads.Models;

namespace PursuitAlert.Domain.Device.Payloads.Events
{
    public class CoordinatesPayloadReceivedEvent : PubSubEvent<DeviceCoordinatesPayload>
    {
    }
}