using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Modes.Events.EventPayloads
{
    public class ModeChangeEventPayload
    {
        #region Properties

        public ModeChangeType ChangeType { get; set; }

        public Mode NewMode { get; set; }

        #endregion Properties
    }
}