using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Models
{
    public class VehiclesResponse
    {
        #region Properties

        public List<Asset> Assets { get; set; }

        #endregion Properties
    }
}