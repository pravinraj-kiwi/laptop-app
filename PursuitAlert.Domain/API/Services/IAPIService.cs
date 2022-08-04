using PursuitAlert.Domain.API.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Domain.API.Services
{
    public interface IAPIService
    {
        #region Methods

        Task<Node> CreateDeviceNode(string serialNumber, int organizationId);

        Task<Asset> CreateVehicle(string unitID, string serialNumber, string organizationcode, string officerName, string secondaryOfficer = null, string notes = null);

        Task<AssetDevice> EnsureAssetDeviceAssociation(string unitId, int assetId, string serialNumber);

        Task<Node> GetDeviceNodeBySerialNumber(string serialNumber);

        Task<Node> GetOrganizationByOrganizationCode(string organizationCode);

        Task<Asset> GetVehicleByUnitId(string unitId);

        Task SetCredentials(string awsKeyId, string awsSecretKey);

        Task UpdateVehicleInfo(string unitID, string serialNumber, string organizationCode, string officerName, string secondaryOfficer = null, string notes = null);

        #endregion Methods
    }
}