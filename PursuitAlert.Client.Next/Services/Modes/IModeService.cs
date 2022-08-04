using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Modes
{
    public interface IModeService
    {
        #region Properties

        List<Mode> ActiveModes { get; }

        bool IsListening { get; }

        List<Mode> ModeConfiguration { get; }

        Mode PatrolMode { get; }

        Mode PowerOffMode { get; }

        #endregion Properties

        #region Methods

        void ListenForEvents();

        void SetConfiguration(List<Mode> modes);

        void StopListeningForEvents();

        #endregion Methods
    }
}