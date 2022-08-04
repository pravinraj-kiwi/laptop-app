using PursuitAlert.Domain.Device.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Device.Services
{
    public interface IDeviceMonitorService
    {
        #region Properties

        bool DeviceIsConnected { get; }

        #endregion Properties

        #region Methods

        void StartDeviceListener(DeviceConnectionParameters parameters, int listenerInterval = 3000);

        void StopDeviceListener();

        #endregion Methods
    }
}