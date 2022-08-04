using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Queries
{
    public class Assets
    {
        #region Fields

        public const string GetAssetById = @"
        query GetAssetById($assetId: Int) {
            assets(where: {id: {_eq: $assetId}}) {
            attributes
            id
            node_id
            status
            }
        }";

        public const string GetVehicleByUnitId = @"
            query GetVehicleByUnitId($unitId: jsonb, $organizationId: Int) {
              assets(where: {attributes: {_contains: $unitId}, _and: {}, node: {parentId: {_eq: $organizationId}}}) {
                id
                attributes
                node {
                  parentId
                }
              }
            }";

        #endregion Fields
    }
}