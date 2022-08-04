using PursuitAlert.Client.Services.Modes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.Events.EventPayloads
{
    public class UnmappedButtonPressedEventPayload
    {
        #region Properties

        public int ButtonPressed { get; set; }

        public List<Mode> ConfiguredModes { get; set; }

        #endregion Properties
    }
}