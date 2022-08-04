using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Sounds
{
    public class SoundPlayerService : ISoundPlayerService
    {
        #region Fields

        private SoundPlayer ModeDisengagedPlayer;

        private SoundPlayer ModeEngagedPlayer;

        private SoundPlayer PinDropPlayer;

        private SoundPlayer SuccessPlayer;

        private SoundPlayer WarningPlayer;

        #endregion Fields

        #region Constructors

        public SoundPlayerService()
        {
            var soundFileNameFormat = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sounds", "{0}.wav");

            ModeEngagedPlayer = new SoundPlayer(Properties.Sounds.Engage);
            ModeEngagedPlayer.Load();

            ModeDisengagedPlayer = new SoundPlayer(Properties.Sounds.Disengage);
            ModeDisengagedPlayer.Load();

            PinDropPlayer = new SoundPlayer(Properties.Sounds.PinDrop);
            PinDropPlayer.Load();

            WarningPlayer = new SoundPlayer(Properties.Sounds.Warning);
            WarningPlayer.Load();

            SuccessPlayer = new SoundPlayer(Properties.Sounds.Success);
            SuccessPlayer.Load();
        }

        #endregion Constructors

        #region Methods

        public void PlayModeDisengagedSound() => ModeDisengagedPlayer.Play();

        public void PlayModeEngagedSound() => ModeEngagedPlayer.Play();

        public void PlayPinDropSound() => PinDropPlayer.Play();

        public void PlaySuccessSound() => SuccessPlayer.Play();

        public void PlayWarningSound() => WarningPlayer.Play();

        #endregion Methods
    }
}