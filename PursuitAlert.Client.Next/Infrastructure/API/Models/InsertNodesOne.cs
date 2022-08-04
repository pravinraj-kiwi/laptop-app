using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Models
{
    public class InsertNodesOne
    {
        #region Properties

        [JsonProperty("insert_nodes_one")]
        public Node Node { get; set; }

        #endregion Properties
    }
}