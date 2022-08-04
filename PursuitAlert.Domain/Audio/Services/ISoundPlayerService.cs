using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Audio.Services
{
    public interface ISoundPlayerService
    {
        #region Methods

        void PlayModeDisengagedSound();

        void PlayModeEngagedSound();

        void PlayPinDropSound();

        void PlaySuccessSound();

        void PlayWarningSound();

        #endregion Methods
    }
}