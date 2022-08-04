using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Domain.Publishing.Services
{
    public interface IPublishingService
    {
        bool IsConnected { get; }
        bool IsConnecting { get; }
        #region Methods

        Task SendLogMessage(string message, int Qos = 1);

        Task SendTelemetry(string message, int Qos = 1);

        Task SetCredentials(string accessKeyId, string secretAccessKey, string brokerUrl, string serialNumber, string stage);

        #endregion Methods
    }
}