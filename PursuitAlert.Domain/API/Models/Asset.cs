using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Models
{
    public class Asset
    {
        #region Properties

        public JObject Attributes { get; set; }

        public int Id { get; set; }

        public Node Node { get; set; }

        #endregion Properties
    }
}