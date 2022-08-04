using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Queries
{
    public class AssetDevices
    {
        #region Fields

        public const string CreateVehicleDeviceAssociationMutation = @"
            mutation CreateVehicleDeviceAssociationMutation($assetId: Int, $deviceNodeId: Int, $createdBy: String) {
              insert_asset_devices(objects: {asset_id: $assetId, device_node_id: $deviceNodeId, created_by: $createdBy}) {
                returning {
                  asset_id
                  device_node_id
                }
              }
            }";

        public const string DeleteVehicleDeviceAssociationMutation = @"
            mutation DeleteVehicleDeviceAssociationMutation($deviceNodeId: Int) {
              delete_asset_devices(where: {device_node_id: {_eq: $deviceNodeId}}) {
                affected_rows
              }
            }";

        public const string GetVehicleDeviceAssociationForDevice = @"
            query GetVehicleDeviceAssociationForDevice($deviceNodeId: Int) {
                asset_devices(where: {device_node_id: {_eq: $deviceNodeId}}) {
                    asset_id
                    device_node_id
                    id
                }
            }";

        public const string UpdateVehicleDeviceAssociationForDevice = @"
            mutation UpdateVehicleDeviceAssociationForDevice($deviceNodeId: Int, $assetId: Int, $updatedBy: String) {
              update_asset_devices(where: {device_node_id: {_eq: $deviceNodeId}}, _set: {asset_id: $assetId, updated_by: $updatedBy}) {
                returning {
                  asset_id
                  device_node_id
                  id
                }
              }
            }";

        #endregion Fields
    }
}