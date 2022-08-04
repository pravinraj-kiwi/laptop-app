using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Client.Infrastructure.API.Queries
{
    public class Nodes
    {
        #region Fields

        public const string GetDeviceNodeBySerialNumber = @"
            query GetDeviceNodeBySerialNumber($deviceSerialNumber: String) {
              nodes(where: {hardwareId: {_eq: $deviceSerialNumber}}) {
                id
                parentId
              }
            }";

        public const string GetOrganizationByCode = @"
            query GetOrganizationByCode($organizationCode: jsonb) {
                nodes(where: {properties: {_contains: $organizationCode}}) {
                  properties
                  id
                }
            }";

        #endregion Fields
    }
}