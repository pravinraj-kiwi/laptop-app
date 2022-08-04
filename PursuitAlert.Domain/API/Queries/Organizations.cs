using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Queries
{
    public class Organizations
    {
        #region Fields

        public const string GetOrganizationsByInstallCodeQuery = @"
            query GetOrganizationsByInstallCodeQuery($organizationCode: jsonb) {
                organizations(where: {node: {properties: {_contains: $organizationCode}}}, limit: 1) {
                node {
                    properties
                    id
                }
              }
            }";

        #endregion Fields
    }
}