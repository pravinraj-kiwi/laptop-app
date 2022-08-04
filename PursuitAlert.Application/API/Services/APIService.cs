using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using GraphQL.Client.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PursuitAlert.Domain.API.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using GraphQL.Client.Serializer.Newtonsoft;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using System.Diagnostics;
using PursuitAlert.Domain.API.Models;
using Polly;
using Polly.Retry;
using PursuitAlert.Domain.API.Queries;
using PursuitAlert.Domain.API.Errors;
using System.Security.Principal;

namespace PursuitAlert.Application.API.Services
{
    public class APIService : IAPIService
    {
        #region Properties

        private string CurrentUser => string.Format("windowsappuser_{0}", WindowsIdentity.GetCurrent().Name.ToLower());

        #endregion Properties

        #region Fields

        private const string AuthenticationEndpointKey = "authentication-endpoint";

        private const string GraphQLEndpointKey = "graph-ql-api-endpoint";

        private const string ServiceAccountClientIdKey = "windows-app-service-account-client-id";

        private const string ServiceAccountPasswordKey = "windows-app-service-account-password";

        private const string ServiceAccountUsernameKey = "windows-app-service-account-username";

        private string _authenticationEndpoint;

        private string _awsKeyId;

        private string _awsSecretKey;

        private GraphQLHttpClient _client;

        private string _clientId;

        private string _password;

        private AsyncRetryPolicy _queryAsyncRetryPolicy;

        private string _username;

        #endregion Fields

        #region Constructors

        public APIService()
        {
            _queryAsyncRetryPolicy = Policy
                .Handle<NullReferenceException>()
                .WaitAndRetryAsync(4,
                retryAttempt => TimeSpan.FromSeconds(Math.Min(1, retryAttempt - 1)),
                (ex, retryTime) => Log.Warning("Failed to send query to GraphQL. Retrying in {retryTime}. {errorMessage}", retryTime, ex.Message));
        }

        #endregion Constructors

        #region Methods

        public async Task<Node> CreateDeviceNode(string serialNumber, int organizationId)
        {
            Log.Verbose("Creating device node with serial number {serialNumber} and organization Id {organizationId}", serialNumber, organizationId);
            var properties = new JObject
            {
                { "WindowsUser", CurrentUser }
            };
            var nodeResult = await SendMutation<InsertNodesOne>(nameof(Nodes.CreateDeviceNodeMutation), Nodes.CreateDeviceNodeMutation, new { serialNumber, type = "Device", parentId = organizationId, properties });
            Log.Debug("Device node {id} created with serial number {serialNumber} for organization {organizationId}", nodeResult.Node.Id, serialNumber, organizationId);
            return nodeResult.Node;
        }

        public async Task<Asset> CreateVehicle(string unitID, string serialNumber, string organizationcode, string officerName, string secondaryOfficer = null, string notes = null)
        {
            Log.Debug("Attempting to create vehicle with unit Id {unitId} (details={details})", unitID, JsonConvert.SerializeObject(new { unitID, organizationcode, officerName, secondaryOfficer, notes }));
            var vehicleTask = GetVehicleByUnitId(unitID);
            var organizationInfoTask = GetOrganizationByOrganizationCode(organizationcode);

            await Task.WhenAll(vehicleTask, organizationInfoTask);

            var vehicle = await vehicleTask;
            var organization = await organizationInfoTask;

            if (vehicle != null)
            {
                Log.Warning("Existing vehicle found with unit Id {unitId}", unitID);
                throw new VehicleExistsException(unitID);
            }

            var nodeName = $"Unit {organization.Id}-{unitID.Replace("unit", string.Empty).Replace("Unit", string.Empty).Trim()}";
            Log.Verbose("Creating node for vehicle {unitId} and organization Id {organizationId}", unitID, organization.Id);
            var properties = new JObject
            {
                { "WindowsUser", CurrentUser }
            };
            var nodeResult = await SendMutation<InsertNodesOne>(nameof(Nodes.CreateVehicleNodeMutation), Nodes.CreateVehicleNodeMutation, new { nodeName, type = "Asset", parentId = organization.Id, properties });
            Log.Debug("Device node {id} created with serial number {serialNumber} for organization {organizationId}", nodeResult.Node.Id, serialNumber, organization.Id);
            var node = nodeResult.Node;
            var attributes = new JObject
            {
                { "Unit ID", unitID },
                { "Primary Officer", officerName },
                { "Secondary Officer", secondaryOfficer ?? string.Empty },
                { "Notes", notes ?? string.Empty },
                { "K9", string.Empty },
                { "Name", $"Unit {unitID.Replace("unit", string.Empty).Replace("Unit", string.Empty).Trim()}" },
                { "Model", string.Empty }
            };
            vehicle = await CreateAsset(node.Id, CurrentUser, attributes);
            Log.Debug("Vehicle {unitId} created successfully", unitID);

            await EnsureAssetDeviceAssociation(unitID, vehicle.Id, serialNumber);

            return vehicle;
        }

        public async Task<AssetDevice> EnsureAssetDeviceAssociation(string unitId, int assetId, string serialNumber)
        {
            var deviceNodeResponse = await SendQuery<NodeList>(nameof(Nodes.GetDeviceNodeBySerialNumber), Nodes.GetDeviceNodeBySerialNumber, new { serialNumber });
            var deviceNode = deviceNodeResponse.Nodes.First();

            var assetDeviceMappingResult = await SendQuery<AssetDeviceList>(nameof(AssetDevices.GetVehicleDeviceAssociationForDevice), AssetDevices.GetVehicleDeviceAssociationForDevice, new { deviceNodeId = deviceNode.Id });
            var assetDeviceMapping = assetDeviceMappingResult.AssetDevices.FirstOrDefault();
            if (assetDeviceMapping == null || assetDeviceMapping == null)
            {
                // A mapping doesn't exist for this vehicle, we need to create one
                Log.Verbose("No asset/device mapping for unit Id {unitId} ({assetId}) found. Creating one.", unitId, assetId);
                var newMapping = await SendMutation<AssetDevice>(nameof(AssetDevices.CreateVehicleDeviceAssociationMutation), AssetDevices.CreateVehicleDeviceAssociationMutation, new { assetId = assetId, deviceNodeId = deviceNode.Id, createdBy = CurrentUser });
                return newMapping;
            }
            else
            {
                // Make sure the mapping is correct, if it's not update it
                if (assetDeviceMapping.DeviceNodeId != deviceNode.Id || assetDeviceMapping.AssetId != assetId)
                {
                    // We have to do this in 2 steps (e.g. delete, then recreate) because GraphQL
                    // doesn't appear to be able to handle the on_confict parameter
                    Log.Verbose("Vehicle unit Id {unitId} is currently mapped to device Id {deviceId}. Updating the mapping to reflect the current device ({newDeviceId})", unitId, assetDeviceMapping.DeviceNodeId, deviceNode.Id);
                    var newMapping = await SendMutation<AssetDevice>(nameof(AssetDevices.UpdateVehicleDeviceAssociationForDevice), AssetDevices.UpdateVehicleDeviceAssociationForDevice, new { deviceNodeId = deviceNode.Id, assetId, updatedBy = CurrentUser });
                    Log.Verbose("Vehicle unit Id {unitId} mapping to device {deviceId} created successfully", unitId, newMapping.DeviceNodeId);
                    return newMapping;
                }
                else
                {
                    Log.Verbose("Existing device mapping is correct. Device {serialNumber} ({deviceId}) is reporting for vehicle {unitId} ({assetId})", serialNumber, deviceNode.Id, unitId, assetId);
                    return assetDeviceMapping;
                }
            }
        }

        public async Task<Node> GetDeviceNodeBySerialNumber(string serialNumber)
        {
            Log.Verbose("Attempting to get device node using serial number {serialNumber}", serialNumber);
            var nodeResult = await SendQuery<NodeList>(nameof(Nodes.GetDeviceNodeBySerialNumber), Nodes.GetDeviceNodeBySerialNumber, new { serialNumber });
            if (nodeResult.Nodes == null || nodeResult.Nodes.Count == 0)
            {
                Log.Debug("No node found with serial number {serialNumber}", serialNumber);
                return null;
            }
            else
            {
                var node = nodeResult.Nodes.First();
                Log.Debug("Node Id {id} found with serial number {serialNumber}", node.Id, serialNumber);
                return node;
            }
        }

        public async Task<Node> GetOrganizationByOrganizationCode(string organizationCode)
        {
            Log.Debug("Attempting to find organization with install code {organizationCode}", organizationCode);
            var organizationCodeObject = new JObject
            {
                { "Install Code", organizationCode }
            };
            var organization = await SendQuery<OrganizationsResponse>(nameof(Organizations.GetOrganizationsByInstallCodeQuery), Organizations.GetOrganizationsByInstallCodeQuery, new { organizationCode = organizationCodeObject });
            if (organization == null || organization.Organizations.Count == 0)
            {
                Log.Verbose("No organization found with install code {organizationCode}", organizationCode);
                return null;
            }
            else
            {
                Log.Verbose("Organization id {organizationId} ({organizationName}) found with install code {organizationCode}", organization.Organizations.First().Node.Id, organization.Organizations.First().Node.Properties["Display Name"].Value<string>(), organizationCode);
                return organization.Organizations.First().Node;
            }
        }

        public async Task<Asset> GetVehicleByUnitId(string unitId)
        {
            Log.Debug("Attempting to get vehicle with unit Id {unitId}", unitId);
            var unitIdFilter = new JObject
            {
                { "Unit ID", unitId }
            };
            var vehicles = await SendQuery<VehiclesResponse>(nameof(Vehicles.GetVehicleByUnitIdQuery), Vehicles.GetVehicleByUnitIdQuery, new { unitId = unitIdFilter });
            if (vehicles == null || vehicles.Assets == null || vehicles.Assets.Count == 0)
            {
                Log.Verbose("No vehicle found with unit Id {unitId}", unitId);
                return null;
            }
            Log.Debug("Found asset Id {vehicleId} ({vehicleName}) with unit Id {unitId}", vehicles.Assets.First().Id, vehicles.Assets.First().Attributes["Name"].ToString(), unitId);
            return vehicles.Assets.First();
        }

        public async Task<T> SendMutation<T>(string operationName, string queryText, object variables = null)
        {
            var request = new GraphQLHttpRequest
            {
                Query = queryText,
                OperationName = operationName,
                Variables = variables
            };
            var response = await _queryAsyncRetryPolicy.ExecuteAsync(() => _client.SendMutationAsync<T>(request));
            if (response.Errors != null && response.Errors.Length > 0)
            {
                Log.Error("Reived errors from GraphQL during mutation");
                foreach (var error in response.Errors)
                    Log.Verbose("{errorCode}: {errorMessage} ({extensions})", error.Path, error.Message, error.Extensions == null || error.Extensions.Count == 0 ? string.Empty : string.Join(", ", error.Extensions.Select(e => $"{e.Key}: {e.Value}")).Trim().TrimEnd(','));
                throw new Exception(response.Errors[0].Message);
            }
            return response.Data;
        }

        public async Task<T> SendQuery<T>(string operationName, string queryText, object variables = null)
        {
            var request = new GraphQLHttpRequest
            {
                Query = queryText,
                OperationName = operationName,
                Variables = variables
            };
            var response = await _queryAsyncRetryPolicy.ExecuteAsync(() => _client.SendQueryAsync<T>(request));
            if (response.Errors != null && response.Errors.Length > 0)
            {
                Log.Error("Reived errors from GraphQL during query");
                foreach (var error in response.Errors)
                    Log.Verbose("{errorCode}: {errorMessage} ({extensions})", error.Path, error.Message, error.Extensions == null || error.Extensions.Count == 0 ? string.Empty : string.Join(", ", error.Extensions.Select(e => $"{e.Key}: {e.Value}")).Trim().TrimEnd(','));
                throw new Exception(response.Errors[0].Message);
            }
            return response.Data;
        }

        public async Task SetCredentials(string awsKeyId, string awsSecretKey)
        {
            _awsKeyId = awsKeyId;
            _awsSecretKey = awsSecretKey;

            Log.Debug("Authenticating with SSM");
            var ssm = new AmazonSimpleSystemsManagementClient(_awsKeyId, _awsSecretKey);
            Log.Verbose("Authenticated with SSM successfully");

            try
            {
                Log.Debug("Getting paramters from SSM");
                var parameters = await ssm.GetParametersAsync(new GetParametersRequest
                {
                    Names = new List<string>
                {
                    ServiceAccountUsernameKey,
                    ServiceAccountPasswordKey,
                    GraphQLEndpointKey,
                    AuthenticationEndpointKey,
                    ServiceAccountClientIdKey
                },
                    WithDecryption = true
                }, default)
                    .ConfigureAwait(false);
                Log.Verbose("{paramterCount} parameter(s) retrieved from SSM successfully", parameters == null || parameters.Parameters == null ? 0 : parameters.Parameters.Count);

                var username = parameters.Parameters.First(p => p.Name.Equals(ServiceAccountUsernameKey));
                var password = parameters.Parameters.First(p => p.Name.Equals(ServiceAccountPasswordKey));
                var clientId = parameters.Parameters.First(p => p.Name.Equals(ServiceAccountClientIdKey));
                var apiBaseUrl = parameters.Parameters.First(p => p.Name.Equals(GraphQLEndpointKey));
                var authenticationEndpoint = parameters.Parameters.First(p => p.Name.Equals(AuthenticationEndpointKey));

                _username = username.Value;
                _password = password.Value;
                _clientId = clientId.Value;
                _authenticationEndpoint = authenticationEndpoint.Value;

                Log.Debug("Authenticating with Cognito");
                var cognito = new AmazonCognitoIdentityProviderClient(_awsKeyId, _awsSecretKey);
                Log.Verbose("Authenticated with Cognito");
                Log.Debug("Getting ID token from Cognito for the service account retrieved from SSM.");
                var authResponse = await cognito.InitiateAuthAsync(new InitiateAuthRequest
                {
                    AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                    AuthParameters = new Dictionary<string, string>
                    {
                        ["USERNAME"] = _username,
                        ["PASSWORD"] = _password
                    },
                    ClientId = _clientId
                });

                var idToken = authResponse.AuthenticationResult.IdToken;
                Log.Verbose("ID token retrieved successfully from Cognito");

                var httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl.Value) };
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
                _client = new GraphQLHttpClient(new GraphQLHttpClientOptions(), new NewtonsoftJsonSerializer(), httpClient);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get parameters from SSM");
            }
        }

        public async Task UpdateVehicleInfo(string unitID, string serialNumber, string organizationCode, string officerName, string secondaryOfficer = null, string notes = null)
        {
            Log.Verbose("Attempting to update vehicle info for unit {unitId}, (details={details})", unitID, JsonConvert.SerializeObject(new { unitID, organizationCode, officerName, secondaryOfficer, notes }));

            // First, get the existing vehicle attributes so that we can do a pure "update" and not
            // a replace. jsonb columns can only be updated enmasse and not by individual properties
            var vehicleTask = GetVehicleByUnitId(unitID);
            var organizationInfoTask = GetOrganizationByOrganizationCode(organizationCode);

            await Task.WhenAll(vehicleTask, organizationInfoTask);

            var vehicle = await vehicleTask;
            var updateObject = vehicle.Attributes;
            var organization = await organizationInfoTask;

            // Update the values in the existing schema, while keeping anything else that's there
            updateObject["Notes"] = notes;
            updateObject["Primary Officer"] = officerName;
            updateObject["Secondary Officer"] = secondaryOfficer;

            // If we're changing the organization, update the query to do that
            var query = string.Empty;
            var operationName = string.Empty;
            if (vehicle.Node.ParentId != organization.Id)
            {
                Log.Debug("Updating vehicle organization from {originalOrganizationId} to {newOrganizationId}", vehicle.Node.ParentId, organization.Id);
                query = Vehicles.UpdateVehicleAttributesAndOrganizationIdMutation;
                operationName = nameof(Vehicles.UpdateVehicleAttributesAndOrganizationIdMutation);
            }
            else
            {
                query = Vehicles.UpdateVehicleAttributesByAssetIdMutation;
                operationName = nameof(Vehicles.UpdateVehicleAttributesByAssetIdMutation);
            }

            var request = new GraphQLHttpRequest
            {
                Query = query,
                OperationName = operationName,
                Variables = new
                {
                    assetId = vehicle.Id,
                    attributes = updateObject
                }
            };
            var response = await _queryAsyncRetryPolicy.ExecuteAsync(() => _client.SendQueryAsync<VehiclesResponse>(request));
            Log.Debug("Updated vehicle info for vehicle {unitId} successfully", unitID);

            await EnsureAssetDeviceAssociation(unitID, vehicle.Id, serialNumber);
        }

        private async Task<Asset> CreateAsset(int nodeId, string createdBy, JObject attributes)
        {
            Log.Verbose("Creating asset with nodeId: {nodeId}, createdBy: {createdBy}, and attributes: {attributes}", nodeId, createdBy, attributes.ToString());
            var assetResult = await SendMutation<InsertAssetsOne>(nameof(Vehicles.CreateVehicleMutation), Vehicles.CreateVehicleMutation, new { nodeId, createdBy, attributes });
            Log.Debug("Asset Id {id} created successfully", assetResult.Asset.Id);
            return assetResult.Asset;
        }

        #endregion Methods
    }
}