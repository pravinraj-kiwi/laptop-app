using PursuitAlert.Domain.Modes.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Configuration.Models
{
    public class DeviceConfigurationRetrievedEventArgs
    {
        #region Properties

        public string FromURL { get; set; }

        public List<Mode> Modes { get; set; }

        public DateTime RetrievedAt { get; set; }

        #endregion Properties
    }
}