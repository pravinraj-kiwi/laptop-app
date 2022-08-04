using PursuitAlert.Domain.Modes.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Modes.Services
{
    public interface IModeService
    {
        #region Properties

        List<Mode> ModeConfiguration { get; }

        bool PatrolModeActive { get; }

        #endregion Properties

        #region Methods

        void SetSwitchMapping(List<Mode> modes);

        #endregion Methods
    }
}