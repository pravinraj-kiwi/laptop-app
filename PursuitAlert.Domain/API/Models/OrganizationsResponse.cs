using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Models
{
    public class Organization
    {
        #region Properties

        public Node Node { get; set; }

        #endregion Properties
    }

    public class OrganizationsResponse
    {
        #region Properties

        public List<Organization> Organizations { get; set; }

        #endregion Properties
    }
}