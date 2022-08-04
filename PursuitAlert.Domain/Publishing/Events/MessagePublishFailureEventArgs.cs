using System;

namespace PursuitAlert.Domain.Publishing.Events
{
    public class MessagePublishFailureEventArgs
    {
        #region Properties

        public Exception Exception { get; set; }

        public string FailureReason { get; set; }

        public string Message { get; set; }

        #endregion Properties
    }
}