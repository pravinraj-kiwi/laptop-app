using PursuitAlert.Domain.Modes.Models;
using System.Collections.Generic;

namespace PursuitAlert.Domain.Modes.Events
{
    public class ModeChangeEventArgs
    {
        #region Properties

        /// <summary>
        /// A list of all activated modes. Used by viewmodels to determine how multiple active modes
        /// should be displayed to the user
        /// </summary>
        public List<Mode> ActivatedModes { get; set; }

        /// <summary>
        /// The type of change that occurred
        /// </summary>
        public ModeChangeType ChangeType { get; set; }

        /// <summary>
        /// The mode that resulted from the <see cref="ModeChangeEvent" />
        /// </summary>
        public Mode NewMode { get; set; }

        /// <summary>
        /// The mode before the <see cref="ModeChangeEvent" />
        /// </summary>
        public Mode OriginalMode { get; set; }

        #endregion Properties
    }
}