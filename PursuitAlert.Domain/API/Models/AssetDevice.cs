using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Models
{
    public class AssetDevice
    {
        #region Properties

        [JsonProperty("asset_id")]
        public int AssetId { get; set; }

        [JsonProperty("device_node_id")]
        public int DeviceNodeId { get; set; }

        public int Id { get; set; }

        #endregion Properties
    }
}