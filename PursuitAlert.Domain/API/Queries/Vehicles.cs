using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Queries
{
    public class Vehicles
    {
        #region Fields

        public const string CreateVehicleMutation = @"
            mutation CreateVehicleMutation($nodeId: Int, $createdBy: String, $attributes: jsonb) {
              insert_assets_one(object: {node_id: $nodeId, asset_model_id: 1, status: ""ACTIVE"", created_by: $createdBy, attributes: $attributes}) {
                id
                }
            }";

        public const string GetVehicleByUnitIdQuery = @"
                    query GetVehicleByUnitIdQuery($unitId: jsonb) {
                        assets(where: {attributes: {_contains: $unitId}}) {
                            id
                            attributes,
                            node {
                                parentId
                            }
                        }
                    }";

        public const string UpdateVehicleAttributesAndOrganizationIdMutation = @"
                mutation UpdateVehicleAttributesAndOrganizationIdMutation($assetId: Int, $attributes: jsonb, $organizationId: Int) {
                  update_assets(where: {id: {_eq: $assetId}}, _set: {attributes: $attributes}) {
                    returning {
                      attributes
                    }
                  }
                  update_nodes(where: {id: {_eq: $assetId}}, _set: {parentId: $organizationId}) {
                    affected_rows
                  }
                }";

        public const string UpdateVehicleAttributesByAssetIdMutation = @"
            mutation UpdateVehicleAttributesByAssetIdMutation($assetId: Int, $attributes: jsonb) {
                update_assets(where: {id: {_eq: $assetId}}, _set: {attributes: $attributes}) {
                returning {
                    attributes
                }
                }
            }";

        #endregion Fields
    }
}