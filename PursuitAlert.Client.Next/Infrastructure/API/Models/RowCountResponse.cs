using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Models
{
    public class RowCountResponse
    {
        #region Properties

        [JsonProperty("affected_rows")]
        public int AffectedRows { get; set; }

        #endregion Properties
    }
}