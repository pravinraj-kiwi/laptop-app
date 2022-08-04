using PursuitAlert.Domain.Device.Payloads.Models;
using PursuitAlert.Domain.Modes.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Publishing.Services
{
    public interface IMessageBuilderService
    {
        #region Methods

        string BuildMessage(Mode mode, DeviceCoordinatesPayload coordinates, bool constructEventClearMessage = false);

        #endregion Methods
    }
}