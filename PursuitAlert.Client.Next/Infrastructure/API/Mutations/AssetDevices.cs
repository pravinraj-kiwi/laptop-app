using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Mutations
{
    public class AssetDevices
    {
        #region Fields

        public const string CreateVehicleDeviceAssociationMutation = @"
            mutation CreateVehicleDeviceAssociationMutation($assetId: Int, $deviceNodeId: Int) {
              insert_asset_devices(objects: {asset_id: $assetId, device_node_id: $deviceNodeId}) {
                returning {
                  asset_id
                  device_node_id
                }
              }
            }";

        public const string DeleteVehicleDeviceAssociationMutation = @"
            mutation DeleteVehicleDeviceAssociationMutation($mappingId: Int) {
                delete_asset_devices(where: {device_node_id: {_eq: $mappingId}}) {
                    affected_rows
                }
            }";

        public const string UpdateVehicleDeviceAssociationForDevice = @"
            mutation UpdateVehicleDeviceAssociationForDevice($mappingId: Int, $deviceNodeId: Int) {
              update_asset_devices(where: {id: {_eq: $mappingId}}, _set: {device_node_id: $deviceNodeId}) {
                returning {
                  asset_id
                  device_node_id
                  id
                }
              }
            }";

        public const string UpdateVehicleDeviceAssociationForVehicle = @"
            mutation UpdateVehicleDeviceAssociationForVehicle($mappingId: Int, $assetId: Int) {
              update_asset_devices(where: {id: {_eq: $mappingId}}, _set: {asset_id: $assetId}) {
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