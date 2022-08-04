using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Queries
{
    public class AssetDevices
    {
        #region Fields

        public const string GetVehicleAssociatedWithDevice = @"
            query GetVehicleAssociatedWithDevice($assetId: Int, $deviceNodeId: Int) {
              asset_devices(where: {_or: [{asset_id: {_eq: $assetId}}, {device_node_id: {_eq: $deviceNodeId}}]}) {
                id
                asset_id
                device_node_id
              }
            }";

        #endregion Fields
    }
}