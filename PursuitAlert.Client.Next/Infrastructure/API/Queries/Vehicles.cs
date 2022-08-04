using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Queries
{
    public class Vehicles
    {
        #region Fields

        public const string GetVehicleByAssetId = @"
            query GetVehicleByAssetId($assetId: Int) {
              vehicles(where: {id: {_eq: $assetId}}) {
                bearing
                current_status
                hardware_id
                id
                jurisdiction_id
                k9_officer
                last_update
                name
                notes
                officer
                secondary_officer
                speed
                unit_id
                vehicle_model
              }
            }";

        #endregion Fields
    }
}