using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Modes
{
    public class JurisdictionConfiguration
    {
        #region Properties

        /// <summary>
        /// The date when this configuration version was retrieved.
        /// </summary>
        public DateTime RetrievedAt { get; set; }

        #endregion Properties

        #region Fields

        /// <summary>
        /// The list of modes and their button mappings for this jurisdiction
        /// </summary>
        public List<Mode> Modes;

        #endregion Fields
    }
}