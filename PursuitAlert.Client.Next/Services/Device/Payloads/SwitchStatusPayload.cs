using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.Payloads
{
    public class SwitchStatusPayload
    {
        #region Properties

        public int ActivatedButton { get; set; }

        public Dictionary<int, bool> ButtonStatuses { get; set; }

        public float FirmwareVersion { get; set; }

        public string SerialNumber { get; set; }

        #endregion Properties

        #region Fields

        private const string SwitchStatusPreamble = "$SW,";

        #endregion Fields

        #region Methods

        public static SwitchStatusPayload Parse(string data)
        {
            // Ex. $SW,0,0,0,0,0,fsy91itz,0.7
            var result = new SwitchStatusPayload();

            // Replace the preamble
            data = data.Replace(SwitchStatusPreamble, string.Empty);

            // Split into segments
            var payloadPieces = data.Split(new char[] { ',' });

            // Allen Brooks 5/27/2020: Updated after updated device firmware from Raptor with LED
            // intensity options Get the firmware version (always the last item)
            result.FirmwareVersion = float.TryParse(payloadPieces.Last(), out float firmwareVersion) ? firmwareVersion : 0.0f;

            // Allen Brooks 5/27/2020: Updated after updated device firmware from Raptor with LED
            // intensity options Get the serial number (always the next-to-last item)
            result.SerialNumber = payloadPieces[payloadPieces.Length - 2].ToString();

            // Get the activated button
            var activatedButton = -1;
            if (payloadPieces.Any(status => status == "1"))
                activatedButton = Array.IndexOf(payloadPieces, payloadPieces.First(status => status == "1")) + 1;

            // Remove the serial number from the payload to get the button statuses, convert the
            // values to a dictionary of { buttonNumber, isActivated }
            var buttonStatuses =
                payloadPieces.Take(payloadPieces.Length - 1)
                .Select((value, index) => new { value, index })

                // Status is active if the value isn't empty and if it equals 1
                .ToDictionary(status => status.index + 1, status => !string.IsNullOrWhiteSpace(status.value) && status.value == "1");

            result.ActivatedButton = activatedButton;
            result.ButtonStatuses = buttonStatuses;

            return result;
        }

        #endregion Methods
    }
}