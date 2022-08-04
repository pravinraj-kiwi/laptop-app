using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PursuitAlert.Client.Services.GPS;
using PursuitAlert.Client.Services.Modes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Telemetry
{
    public static class SignalBuilder
    {
        #region Fields

        private static Dictionary<string, string> RequiredProperties = new Dictionary<string, string>
        {
            ["Event type"] = "T",
            ["Time"] = "U",
            ["Latitude"] = "L",
            ["Longitude"] = "O",
            ["Speed"] = "S",
            ["Bearing"] = "B"
        };

        #endregion Fields

        #region Methods

        public static string Build(Mode mode, Coordinates coordinates, bool sendDisengageSignal = false)
        {
            var eventType = mode.Type;
            if (sendDisengageSignal)
                eventType = $"X{eventType}";

            var payload = CreatePayload(eventType, coordinates);
            var message = JsonConvert.SerializeObject(payload);

            return message;
        }

        private static JObject CreatePayload(string eventType, Coordinates coordinates)
        {
            var result = new JObject();
            foreach (var property in RequiredProperties)
            {
                switch (property.Value)
                {
                    case "T":
                        result.Add("T", eventType);
                        break;

                    case "U":
                        result.Add("U", DateTime.UtcNow.ToString("O"));
                        break;

                    case "L":
                        result.Add("L", coordinates == null || coordinates.Latitude.IsSerializable() ? coordinates.Latitude : 0.00);
                        break;

                    case "O":
                        result.Add("O", coordinates == null || coordinates.Longitude.IsSerializable() ? coordinates.Longitude : 0.00);
                        break;

                    case "B":
                        result.Add("B", coordinates == null || coordinates.Bearing.IsSerializable() ? Math.Round(coordinates.Bearing, 2) : 0.00);
                        break;

                    case "S":
                        result.Add("S", coordinates == null || coordinates.Speed.IsSerializable() ? Math.Round(coordinates.Speed, 2) : 0.00);
                        break;
                }
            }
            return result;
        }

        #endregion Methods
    }
}