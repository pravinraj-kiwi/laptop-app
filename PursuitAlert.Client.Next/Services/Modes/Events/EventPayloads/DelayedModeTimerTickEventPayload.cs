using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Modes.Events.EventPayloads
{
    public class DelayedModeTimerTickEventPayload
    {
        #region Properties

        public Mode Mode { get; set; }

        public int SecondsRemaining { get; set; }

        public int TotalSeconds { get; set; }

        #endregion Properties
    }
}