using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Sounds
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