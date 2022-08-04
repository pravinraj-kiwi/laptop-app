using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Models
{
    public class Node
    {
        #region Properties

        public int Id { get; set; }

        public int ParentId { get; set; }

        public JObject Properties { get; set; }

        #endregion Properties
    }
}