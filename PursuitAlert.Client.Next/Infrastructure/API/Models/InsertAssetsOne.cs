using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Models
{
    public class InsertAssetsOne
    {
        #region Properties

        [JsonProperty("insert_assets_one")]
        public Asset Asset { get; set; }

        #endregion Properties
    }
}