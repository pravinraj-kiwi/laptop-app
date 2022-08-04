using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Models
{
    public class InsertNodesOne
    {
        #region Properties

        [JsonProperty("insert_nodes_one")]
        public Node Node { get; set; }

        #endregion Properties
    }
}