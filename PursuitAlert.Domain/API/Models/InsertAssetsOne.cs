using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Models
{
    public class InsertAssetsOne
    {
        #region Properties

        [JsonProperty("insert_assets_one")]
        public Asset Asset { get; set; }

        #endregion Properties
    }
}