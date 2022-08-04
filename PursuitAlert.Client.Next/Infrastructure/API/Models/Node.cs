using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Models
{
    public class Node
    {
        #region Properties

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("created_by")]
        public string CreatedBy { get; set; }

        public bool Deleted { get; set; }

        public string HardwareId { get; set; }

        public int Id { get; set; }

        public string Name { get; set; }

        public int ParentId { get; set; }

        public string Path { get; set; }

        public JObject Properties { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("updated_by")]
        public string UpdatedBy { get; set; }

        #endregion Properties
    }
}