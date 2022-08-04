using Newtonsoft.Json;
using System.Collections.Generic;

namespace PursuitAlert.Client.Infrastructure.API.Models
{
    public class UpdatedDevice
    {
        #region Properties

        [JsonProperty("update_nodes_by_pk")]
        public Node Node { get; set; }

        #endregion Properties
    }
}