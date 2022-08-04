using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Models
{
    public class Asset
    {
        #region Properties

        [JsonProperty("asset_model_id")]
        public int AssetModelId { get; set; }

        public JObject Attributes { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("created_by")]
        public string CreatedBy { get; set; }

        public int Id { get; set; }

        public Node Node { get; set; }

        [JsonProperty("node_id")]
        public int NodeId { get; set; }

        public string Status { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("updated_by")]
        public string UpdatedBy { get; set; }

        #endregion Properties
    }
}