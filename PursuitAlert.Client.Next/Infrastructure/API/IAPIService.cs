using PursuitAlert.Client.Infrastructure.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API
{
    public interface IAPIService
    {
        #region Methods

        Task Authenticate(int timeout = 3000, bool throwOnTimeout = false);

        Task<Node> CreateDeviceNode(string deviceSerialNumber, int organizationId);

        Task<Asset> CreateVehicle(string unitID, string serialNumber, string organizationcode, string officerName, string secondaryOfficer = null, string notes = null);

        Task<AssetDevice> EnsureAssetDeviceAssociation(string unitId, int assetId, string serialNumber);

        Task EnsureDeviceNodeIsProperlyConfigured(string deviceSerialNumber);

        Task<Node> GetDeviceNodeBySerialNumber(string serialNumber);

        Task<Node> GetOrganizationByOrganizationCode(string organizationCode);

        Task<Asset> GetVehicleByUnitId(string unitId, int organizationId);

        Task<T> SendMutation<T>(string operationName, string queryText, object variables = null);

        Task<T> SendQuery<T>(string operationName, string queryText, object variables = null);

        Task<Node> UpdateNodeParentId(int nodeId, int parentId);

        Task UpdateVehicleInfo(string unitID, string serialNumber, string organizationCode, string officerName, string secondaryOfficer = null, string notes = null);

        #endregion Methods
    }
}