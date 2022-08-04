using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.IoTData
{
    public interface IIoTDataService
    {
        #region Methods

        Task Authenticate();

        void Send(string topic, string message, int Qos = 1);

        Task SendAsync(string topic, string message, int Qos = 1);

        #endregion Methods
    }
}