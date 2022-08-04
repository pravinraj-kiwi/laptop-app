using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Models
{
    public class Returning<T>
    {
        #region Properties

        [JsonProperty("returning")]
        public T Value { get; set; }

        #endregion Properties
    }
}