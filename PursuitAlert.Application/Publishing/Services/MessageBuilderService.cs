using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PursuitAlert.Domain.Device.Payloads.Models;
using PursuitAlert.Domain.GPS.Services;
using PursuitAlert.Domain.Modes.Models;
using PursuitAlert.Domain.Publishing.Services;
using System;
using System.Collections.Generic;

namespace PursuitAlert.Application.Publishing.Services
{
    public class MessageBuilderService : IMessageBuilderService
    {
        #region Fields

        private readonly Dictionary<string, string> RequiredProperties = new Dictionary<string, string>();

        #endregion Fields

        #region Constructors

        public MessageBuilderService()
        {
            RequiredProperties = new Dictionary<string, string>
            {
                ["Event type"] = "T",
                ["Time"] = "U",
                ["Latitude"] = "L",
                ["Longitude"] = "O",
                ["Speed"] = "S",
                ["Bearing"] = "B"
            };
        }

        #endregion Constructors

        #region Methods

        public string BuildMessage(Mode mode, DeviceCoordinatesPayload coordinates, bool constructEventClearMessage = false)
        {
            var eventType = mode.Type;
            if (constructEventClearMessage)
                eventType = $"X{eventType}";

            var payload = CreateDefaultPayload(eventType, coordinates);
            var message = JsonConvert.SerializeObject(payload);

            return message;
        }

        private JObject CreateDefaultPayload(string type, DeviceCoordinatesPayload coordinates)
        {
            var result = new JObject();
            foreach (var property in RequiredProperties)
            {
                switch (property.Value)
                {
                    case "T":
                        result.Add("T", type);
                        break;

                    case "U":
                        result.Add("U", DateTime.UtcNow.ToString("O"));
                        break;

                    case "L":
                        result.Add("L", coordinates == null || double.IsNaN(coordinates.Latitude) ? 0.00 : Math.Round(coordinates.Latitude, 5));
                        break;

                    case "O":
                        result.Add("O", coordinates == null || double.IsNaN(coordinates.Longitude) ? 0.00 : Math.Round(coordinates.Longitude, 5));
                        break;

                    case "B":
                        result.Add("B", coordinates == null || double.IsNaN(coordinates.Bearing) ? 0.00 : Math.Round(coordinates.Bearing, 2));
                        break;

                    case "S":
                        result.Add("S", coordinates == null || double.IsNaN(coordinates.Speed) ? 0.00 : Math.Round(coordinates.Speed));
                        break;
                }
            }

            return result;
        }

        #endregion Methods
    }
}