using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Models
{
    public class AssetDeviceList
    {
        #region Properties

        [JsonProperty("asset_devices")]
        public List<AssetDevice> AssetDevices { get; set; }

        #endregion Properties
    }
}