using PursuitAlert.Client.Old.Properties;
using PursuitAlert.Domain.Audio.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Old.Audio.Services
{
    /// <summary>
    /// This class has to be defined in the <see cref="Client" /> project and not in the <see
    /// cref="Application" /> project because the <see cref="SoundPlayer" /> class is not supported
    /// outside of .NET Framework.
    /// </summary>
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

            ModeEngagedPlayer = new SoundPlayer(string.Format(soundFileNameFormat, Sounds.Engage));
            ModeEngagedPlayer.Load();

            ModeDisengagedPlayer = new SoundPlayer(string.Format(soundFileNameFormat, Sounds.Disengage));
            ModeDisengagedPlayer.Load();

            PinDropPlayer = new SoundPlayer(string.Format(soundFileNameFormat, Sounds.PinDrop));
            PinDropPlayer.Load();

            WarningPlayer = new SoundPlayer(string.Format(soundFileNameFormat, Sounds.Warning));
            WarningPlayer.Load();

            SuccessPlayer = new SoundPlayer(string.Format(soundFileNameFormat, Sounds.Success));
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