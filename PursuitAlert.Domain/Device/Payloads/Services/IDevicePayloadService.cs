using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Domain.Device.Payloads.Services
{
    public interface IDevicePayloadService
    {
        #region Methods

        Task Process(string payload);

        void SetSwitchActivationJitter(int jitter);

        #endregion Methods
    }
}