using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Models
{
    public class AssetDeviceList
    {
        #region Properties

        [JsonProperty("asset_devices")]
        public List<AssetDevice> AssetDevices { get; set; } = new List<AssetDevice>();

        #endregion Properties
    }
}