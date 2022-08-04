using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Configuration.Services
{
    public interface IConfigurationMonitorService
    {
        #region Methods

        void ListenForChanges(string updateUrl, int listenerInterval = 3600000, DateTime? lastConfigurationRetrievedUTC = null);

        #endregion Methods
    }
}