using PursuitAlert.Domain.Modes.Models;
using System;
using System.Collections.Generic;

namespace PursuitAlert.Domain.Configuration.Models
{
    public class JurisdictionConfiguration
    {
        #region Fields

        /// <summary>
        /// The list of modes and their button mappings for this jurisdiction
        /// </summary>
        public List<Mode> Modes;

        /// <summary>
        /// The date when this configuration version was retrieved.
        /// </summary>
        public DateTime RetrievedAt { get; set; }

        #endregion Fields
    }
}