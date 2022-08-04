using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Models
{
    public class UpdatedAssetDevice
    {
        #region Properties

        [JsonProperty("update_asset_devices")]
        public Returning<List<AssetDevice>> AssetDevices { get; set; }

        #endregion Properties
    }
}