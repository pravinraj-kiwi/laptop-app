using PursuitAlert.Client.Services.GPS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Telemetry
{
    public interface ITelemetryService
    {
        #region Properties

        bool IsListening { get; }

        VehicleStatus VehicleStatus { get; }

        #endregion Properties

        #region Methods

        Task Initialize();

        Task SendPowerOff();

        void StartSendingTelemetry();

        void StopSendingTelemetry(string reason = "");

        #endregion Methods
    }
}