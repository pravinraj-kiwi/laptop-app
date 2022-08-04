using Prism.Events;
using PursuitAlert.Domain.Modes.Models;
using System;

namespace PursuitAlert.Domain.Publishing.Models
{
    public class BackgroundJob
    {
        #region Properties

        public bool IsClearMessage { get; }

        public DateTime? LastMessageSent { get; set; }

        public Mode Mode { get; }

        #endregion Properties

        #region Constructors

        public BackgroundJob(Mode mode, bool isEventClearMessage = false)
        {
            Mode = mode;

            IsClearMessage = isEventClearMessage;
        }

        #endregion Constructors
    }
}