using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Mutations
{
    public class Assets
    {
        #region Fields

        public const string CreateVehicle = @"
            mutation CreateVehicle($nodeId: Int, $createdBy: String, $attributes: jsonb) {
              insert_assets_one(object: {node_id: $nodeId, asset_model_id: 1, status: ""ACTIVE"", created_by: $createdBy, attributes: $attributes}) {
                    id
                }
            }";

        public const string UpdateVehicleAttributesAndOrganizationId = @"
            mutation UpdateVehicleAttributesAndOrganizationId($assetId: Int, $attributes: jsonb, $organizationId: Int) {
                update_assets(where: {id: {_eq: $assetId}}, _set: {attributes: $attributes}) {
                    returning {
                        attributes
                    }
                }
                update_nodes(where: {id: {_eq: $assetId}}, _set: {parentId: $organizationId}) {
                    affected_rows
                }
            }";

        public const string UpdateVehicleAttributesByAssetId = @"
            mutation UpdateVehicleAttributesByAssetId($assetId: Int, $attributes: jsonb) {
                update_assets(where: {id: {_eq: $assetId}}, _set: {attributes: $attributes}) {
                returning {
                    attributes
                }
              }
            }";

        #endregion Fields
    }
}