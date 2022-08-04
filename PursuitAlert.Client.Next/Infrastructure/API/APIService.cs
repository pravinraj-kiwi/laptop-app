using DryIoc;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Retry;
using Prism.Services.Dialogs;
using PursuitAlert.Client.Infrastructure.API.Errors;
using PursuitAlert.Client.Infrastructure.API.Models;
using PursuitAlert.Client.Infrastructure.SSMService;
using PursuitAlert.Client.Infrastructure.Tokens;
using PursuitAlert.Client.Properties;
using PursuitAlert.Client.Services.Security;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using PursuitAlert.Client.Dialogs.SettingsDialog;
using System.Timers;
using Prism.Events;
using PursuitAlert.Client.Events;
using PursuitAlert.Client.Events.Payloads;
using PursuitAlert.Client.Resources.Utilities;

namespace PursuitAlert.Client.Infrastructure.API
{
    public class APIService : IAPIService
    {
        #region Properties

        public Timer AuthenticationTimer { get; private set; }

        private string CurrentUser => string.Format("windowsappuser_{0}", WindowsIdentity.GetCurrent().Name.ToLower());

        #endregion Properties

        #region Fields

        private const string GraphQLEndpointKey = "graph-ql-api-endpoint";

        private const string JWTExpirationToken = "JWTExpired";

        private static readonly object RequestLock = new object();

        private readonly IDialogService _dialogService;

        private readonly IEventAggregator _eventAggregator;

        private readonly AsyncRetryPolicy _queryAsyncRetryPolicy;

        private readonly ISSMService _ssmService;

        private readonly ITokenService _tokenService;

        private GraphQLHttpClient _client;

        #endregion Fields

        #region Constructors

        public APIService(IDialogService dialogService,
            IEventAggregator eventAggregator,
            ITokenService tokenService,
            ISSMService ssmService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _tokenService = tokenService;
            _ssmService = ssmService;

            _queryAsyncRetryPolicy = Policy
                .Handle<NullReferenceException>()
                .WaitAndRetryAsync(4,
                retryAttempt => TimeSpan.FromSeconds(Math.Min(1, retryAttempt - 1)),
                (ex, retryTime) => Log.Warning("Failed to send query to GraphQL. Retrying in {retryTime}. {errorMessage}", retryTime, ex.Message));
        }

        #endregion Constructors

        #region Methods

        public async Task Authenticate(int timeout = 3000, bool throwOnTimeout = false)
        {
            // Throw a timeout exception if the operation takes longer than the specified timeout
            if (throwOnTimeout)
                await AuthenticateInternal().TimeoutAfter(timeout);
            else
            {
                // Start a timer with the specified timeout and dispatch an event to update the UI
                // if it takes longer than expected
                AuthenticationTimer = new Timer(3000);
                AuthenticationTimer.Elapsed += DispatchAuthenticationTrouble;
                AuthenticationTimer.Enabled = true;
                AuthenticationTimer.Start();

                await AuthenticateInternal();

                AuthenticationTimer.Elapsed -= DispatchAuthenticationTrouble;
                AuthenticationTimer.Enabled = false;
                AuthenticationTimer.Stop();
                AuthenticationTimer.Dispose();
            }
        }

        public async Task<Node> CreateDeviceNode(string serialNumber, int organizationId)
        {
            Log.Verbose("Creating device node with serial number {serialNumber} and organization Id {organizationId}", serialNumber, organizationId);
            var properties = new JObject
            {
                { "WindowsUser", CurrentUser }
            };
            var nodeResult = await SendMutation<InsertNodesOne>(nameof(Mutations.Nodes.CreateDeviceNode), Mutations.Nodes.CreateDeviceNode, new { serialNumber, type = "Device", parentId = organizationId, properties });
            Log.Debug("Device node {id} created with serial number {serialNumber} for organization {organizationId}", nodeResult.Node.Id, serialNumber, organizationId);
            return nodeResult.Node;
        }

        public async Task<Asset> CreateVehicle(string unitID, string serialNumber, string organizationcode, string officerName, string secondaryOfficer = null, string notes = null)
        {
            // TODO: Add a check to make sure the device and the vehicle have the same parent Id
            Log.Debug("Attempting to create vehicle with unit Id {unitId} (details={details})", unitID, JsonConvert.SerializeObject(new { unitID, organizationcode, officerName, secondaryOfficer, notes }));
            var organizationInfoTask = GetOrganizationByOrganizationCode(organizationcode);
            var vehicleTask = GetVehicleByUnitId(unitID, organizationInfoTask.Id);

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
            var nodeResult = await SendMutation<InsertNodesOne>(nameof(Mutations.Nodes.CreateVehicleNode), Mutations.Nodes.CreateVehicleNode, new { name = nodeName, type = "Asset", parentId = organization.Id, properties });
            Log.Debug("Vehicle node {id} created for organization {organizationId}", nodeResult.Node.Id, organization.Id);
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

            if (!string.IsNullOrWhiteSpace(serialNumber))
                await EnsureAssetDeviceAssociation(unitID, vehicle.Id, serialNumber);

            return vehicle;
        }

        public async Task<AssetDevice> EnsureAssetDeviceAssociation(string unitId, int assetId, string serialNumber)
        {
            // TODO: Create a dialog window that pops up if the vehicle that's attempting to be associated is currently active
            var deviceNodeResponse = await SendQuery<NodeList>(nameof(Queries.Nodes.GetDeviceNodeBySerialNumber), Queries.Nodes.GetDeviceNodeBySerialNumber, new { deviceSerialNumber = serialNumber });
            var deviceNode = deviceNodeResponse.Nodes.First();

            var assetDeviceMappingResult = await SendQuery<AssetDeviceList>(nameof(Queries.AssetDevices.GetVehicleAssociatedWithDevice), Queries.AssetDevices.GetVehicleAssociatedWithDevice, new { deviceNodeId = deviceNode.Id, assetId = assetId });
            var assetDeviceMapping = assetDeviceMappingResult.AssetDevices.FirstOrDefault();

            // If there is no existing mapping for either the vehicle or the device, create the mapping
            if (assetDeviceMapping == null)
            {
                // A mapping doesn't exist for this vehicle, we need to create one
                Log.Verbose("No asset/device mapping for unit Id {unitId} ({assetId}) found. Creating one.", unitId, assetId);
                var newMapping = await SendMutation<AssetDevice>(nameof(Mutations.AssetDevices.CreateVehicleDeviceAssociationMutation), Mutations.AssetDevices.CreateVehicleDeviceAssociationMutation, new { assetId, deviceNodeId = deviceNode.Id });
                return newMapping;
            }
            else
            {
                if (assetDeviceMapping.DeviceNodeId != deviceNode.Id)
                {
                    // The vehicle is currently mapped to another device, update the mapping
                    Log.Verbose("Vehicle unit Id {unitId} is currently mapped to device Id {deviceId}. Updating the mapping to reflect the current device ({newDeviceId})", unitId, assetDeviceMapping.DeviceNodeId, deviceNode.Id);
                    var newMapping = await SendMutation<UpdatedAssetDevice>(nameof(Mutations.AssetDevices.UpdateVehicleDeviceAssociationForDevice), Mutations.AssetDevices.UpdateVehicleDeviceAssociationForDevice, new { mappingId = assetDeviceMapping.Id, deviceNodeId = deviceNode.Id });
                    Log.Verbose("Vehicle unit Id {unitId} mapping to device {deviceId} created successfully", unitId, newMapping.AssetDevices.Value.First().DeviceNodeId);
                    return newMapping.AssetDevices.Value.First();
                }
                else if (assetDeviceMapping.AssetId != assetId)
                {
                    // This device is currently mapped to another vehicle, we can update the
                    // mapping, but be careful not to pull the rug out from under an existing vehicle

                    // Get the vehicle that is currently mapped to the vehicle
                    var vehicles = await SendQuery<VehicleList>(nameof(Queries.Vehicles.GetVehicleByAssetId), Queries.Vehicles.GetVehicleByAssetId, new { assetId = assetDeviceMapping.AssetId });
                    if (vehicles.Vehicles != null && vehicles.Vehicles.Count > 0 && vehicles.Vehicles.First().LastUpdate.HasValue && DateTime.UtcNow.Subtract(vehicles.Vehicles.First().LastUpdate.Value).TotalMinutes <= 30)
                    {
                        // This device is mapped to a vehicle that has been updated in the last 30 minutes
                        Log.Warning("Device Id {deviceNodeId} ({serialNumber}) is currently mapped to asset {assetId} ({vehicleUnitId}), which is currenlty active. Refusing to removing the asset/device mapping for an active vehicle", deviceNode.Id, serialNumber, assetDeviceMapping.AssetId, unitId);
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            _dialogService.Show(nameof(SettingsDialog), new DialogParameters("code=vehicle_in_use"), null);
                        });
                        return null;
                    }
                    else
                    {
                        // The vehicle currently mapped to the device hasn't been updated in the
                        // last 30 minutes, we're ok to remove the mapping
                        Log.Verbose("Vehicle unit Id {unitId} is currently mapped to device Id {deviceId}. Updating the mapping to reflect the current device ({newDeviceId})", unitId, assetDeviceMapping.DeviceNodeId, deviceNode.Id);
                        var newMapping = await SendMutation<UpdatedAssetDevice>(nameof(Mutations.AssetDevices.UpdateVehicleDeviceAssociationForVehicle), Mutations.AssetDevices.UpdateVehicleDeviceAssociationForVehicle, new { mappingId = assetDeviceMapping.Id, assetId });
                        Log.Verbose("Vehicle unit Id {unitId} mapping to device {deviceId} created successfully", unitId, newMapping.AssetDevices.Value.First().DeviceNodeId);
                        return newMapping.AssetDevices.Value.First();
                    }
                }
                else
                {
                    // The mapping is correct, all good
                    Log.Verbose("Existing device mapping is correct. Device {serialNumber} ({deviceId}) is reporting for vehicle {unitId} ({assetId})", serialNumber, deviceNode.Id, unitId, assetId);
                    return assetDeviceMapping;
                }
            }
        }

        public async Task EnsureDeviceNodeIsProperlyConfigured(string deviceSerialNumber)
        {
            var node = await GetDeviceNodeBySerialNumber(deviceSerialNumber);
            _ = await GetOrganizationByOrganizationCode(OrganizationSettings.Default.Code);
            if (node == null)
                await CreateDeviceNode(deviceSerialNumber, OrganizationSettings.Default.Id);
            else
            {
                // Make sure the device is set up to transmit data to the API
                if (node.ParentId != OrganizationSettings.Default.Id)
                {
                    Log.Debug("This node does not belong to the organization stored in settings ({organizationId}). Moving the device to the correct organization", OrganizationSettings.Default.Id);
                    await UpdateNodeParentId(node.Id, OrganizationSettings.Default.Id);
                    Log.Verbose("Node's parent updated successfully to {organizationId}", OrganizationSettings.Default.Id);
                }
            }
        }

        public async Task<Node> GetDeviceNodeBySerialNumber(string deviceSerialNumber)
        {
            Log.Verbose("Attempting to get device node using serial number {serialNumber}", deviceSerialNumber);
            var nodeResult = await SendQuery<NodeList>(nameof(Queries.Nodes.GetDeviceNodeBySerialNumber), Queries.Nodes.GetDeviceNodeBySerialNumber, new { deviceSerialNumber });
            if (nodeResult.Nodes == null || nodeResult.Nodes.Count == 0)
            {
                Log.Debug("No node found with serial number {serialNumber}", deviceSerialNumber);
                return null;
            }
            else
            {
                var node = nodeResult.Nodes.First();
                Log.Debug("Node Id {id} found with serial number {serialNumber}", node.Id, deviceSerialNumber);
                return node;
            }
        }

        /// <summary>
        /// Also sets the organization settings
        /// </summary>
        /// <param name="organizationCode"></param>
        /// <returns></returns>
        public async Task<Node> GetOrganizationByOrganizationCode(string organizationCode)
        {
            Log.Debug("Attempting to find organization with install code {organizationCode}", organizationCode);
            var organizationCodeObject = new JObject
            {
                { "Install Code", organizationCode }
            };
            var organization = await SendQuery<NodeList>(nameof(Queries.Nodes.GetOrganizationByCode), Queries.Nodes.GetOrganizationByCode, new { organizationCode = organizationCodeObject });
            if (organization == null || organization.Nodes.Count == 0)
            {
                Log.Verbose("No organization found with install code {organizationCode}", organizationCode);
                return null;
            }
            else
            {
                Log.Verbose("Organization id {organizationId} ({organizationName}) found with install code {organizationCode}", organization.Nodes.First().Id, organization.Nodes.First().Properties["Display Name"].Value<string>(), organizationCode);
                return organization.Nodes.First();
            }
        }

        public async Task<Asset> GetVehicleByUnitId(string unitId, int organizationId)
        {
            Log.Debug("Attempting to get vehicle with unit Id {unitId}", unitId);
            var unitIdFilter = new JObject
            {
                { "Unit ID", unitId }
            };
            var vehicles = await SendQuery<AssetList>(nameof(Queries.Assets.GetVehicleByUnitId), Queries.Assets.GetVehicleByUnitId, new { unitId = unitIdFilter, organizationId = organizationId });
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
            if (_client == null)
                throw new InvalidOperationException("API client is not yet initialized");

            var request = new GraphQLHttpRequest
            {
                Query = queryText,
                OperationName = operationName,
                Variables = variables
            };

            try
            {
                var response = await _queryAsyncRetryPolicy.ExecuteAsync(() => _client.SendMutationAsync<T>(request));
                if (response.Errors != null && response.Errors.Length > 0)
                {
                    Log.Error("Received errors from GraphQL during mutation");
                    foreach (var error in response.Errors)
                    {
                        Log.Verbose("{errorCode}: {errorMessage} ({extensions})", error.Path, error.Message, error.Extensions == null || error.Extensions.Count == 0 ? string.Empty : string.Join(", ", error.Extensions.Select(e => $"{e.Key}: {e.Value}")).Trim().TrimEnd(','));
                        if (!string.IsNullOrEmpty(error.Message) && error.Message.ToLower().Contains(JWTExpirationToken.ToLower()))
                        {
                            await Authenticate();

                            // Try this call again with the updated token
                            return await SendMutation<T>(operationName, queryText, variables);
                        }
                    }
                    throw new Exception(response.Errors[0].Message);
                }
                return response.Data;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("The remote name could not be resolved"))
            {
                // If we're not connected to the internet yet, wait on internet to become available,
                // and try again when it's back; Would have preferred to use the
                // _queryAsyncRetryPolicy to handle this retry logic, but that imposes a limit on
                // how many times the retry will be attempted; this should be retried indefinitely
                // until an internet connection becomes avaiable again.
                await WaitForInternetAccess();
                return await SendMutation<T>(operationName, queryText, variables);
            }
        }

        public async Task<T> SendQuery<T>(string operationName, string queryText, object variables = null)
        {
            if (_client == null)
                throw new InvalidOperationException("API client is not yet initialized");

            var request = new GraphQLHttpRequest
            {
                Query = queryText,
                OperationName = operationName,
                Variables = variables
            };

            try
            {
                var response = await _queryAsyncRetryPolicy.ExecuteAsync(() => _client.SendQueryAsync<T>(request));
                if (response.Errors != null && response.Errors.Length > 0)
                {
                    Log.Error("Received errors from GraphQL during query");
                    foreach (var error in response.Errors)
                    {
                        Log.Verbose("{errorCode}: {errorMessage} ({extensions})", error.Path, error.Message, error.Extensions == null || error.Extensions.Count == 0 ? string.Empty : string.Join(", ", error.Extensions.Select(e => $"{e.Key}: {e.Value}")).Trim().TrimEnd(','));
                        if (!string.IsNullOrEmpty(error.Message) && error.Message.ToLower().Contains(JWTExpirationToken.ToLower()))
                        {
                            await Authenticate();

                            // Try this call again with the updated token
                            return await SendQuery<T>(operationName, queryText, variables);
                        }
                    }
                    throw new Exception(response.Errors[0].Message);
                }
                return response.Data;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("The remote name could not be resolved"))
            {
                // If we're not connected to the internet yet, wait on internet to become available,
                // and try again when it's back; Would have preferred to use the
                // _queryAsyncRetryPolicy to handle this retry logic, but that imposes a limit on
                // how many times the retry will be attempted; this should be retried indefinitely
                // until an internet connection becomes avaiable again.
                await WaitForInternetAccess();
                return await SendQuery<T>(operationName, queryText, variables);
            }
        }

        public async Task<Node> UpdateNodeParentId(int nodeId, int parentId)
        {
            Log.Verbose("Updating parent Id to {parentId} for node Id {nodeId}", parentId, nodeId);
            var nodeResult = await SendMutation<UpdatedDevice>(nameof(Mutations.Nodes.UpdateNodeParentId), Mutations.Nodes.UpdateNodeParentId, new { nodeId, parentId });
            Log.Debug("Node Id {nodeId} updated with parent Id {parentId}", nodeResult.Node.Id, parentId);
            return nodeResult.Node;
        }

        public async Task UpdateVehicleInfo(string unitID, string serialNumber, string organizationCode, string officerName, string secondaryOfficer = null, string notes = null)
        {
            Log.Verbose("Attempting to update vehicle info for unit {unitId}, (details={details})", unitID, JsonConvert.SerializeObject(new { unitID, organizationCode, officerName, secondaryOfficer, notes }));

            // First, get the existing vehicle attributes so that we can do a pure "update" and not
            // a replace. jsonb columns can only be updated enmasse and not by individual properties
            var organizationInfoTask = GetOrganizationByOrganizationCode(organizationCode);
            var vehicleTask = GetVehicleByUnitId(unitID, organizationInfoTask.Id);

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
                query = Mutations.Assets.UpdateVehicleAttributesAndOrganizationId;
                operationName = nameof(Mutations.Assets.UpdateVehicleAttributesAndOrganizationId);
            }
            else
            {
                query = Mutations.Assets.UpdateVehicleAttributesByAssetId;
                operationName = nameof(Mutations.Assets.UpdateVehicleAttributesByAssetId);
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
            var response = await _queryAsyncRetryPolicy.ExecuteAsync(() => _client.SendQueryAsync<AssetList>(request));
            Log.Debug("Updated vehicle info for vehicle {unitId} successfully", unitID);

            await EnsureAssetDeviceAssociation(unitID, vehicle.Id, serialNumber);
        }

        private async Task AuthenticateInternal()
        {
            var idToken = await _tokenService.GetServiceAccountToken();

            // ! Always use ConfigureAwait with any async calls to AWS. They never return otherwise
            var apiEndpoint = await _ssmService.GetParameter(GraphQLEndpointKey, true).ConfigureAwait(false);

            lock (RequestLock)
            {
                var httpClient = new HttpClient()
                {
                    BaseAddress = new Uri(apiEndpoint.Value)
                };
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
                _client = new GraphQLHttpClient(new GraphQLHttpClientOptions(), new NewtonsoftJsonSerializer(), httpClient);
            }
        }

        private async Task<Asset> CreateAsset(int nodeId, string createdBy, JObject attributes)
        {
            Log.Verbose("Creating asset with nodeId: {nodeId}, createdBy: {createdBy}, and attributes: {attributes}", nodeId, createdBy, attributes.ToString());
            var assetResult = await SendMutation<InsertAssetsOne>(nameof(Mutations.Assets.CreateVehicle), Mutations.Assets.CreateVehicle, new { nodeId, createdBy, attributes });
            Log.Debug("Asset Id {id} created successfully", assetResult.Asset.Id);
            return assetResult.Asset;
        }

        private void DispatchAuthenticationTrouble(object sender, ElapsedEventArgs e)
        {
            _eventAggregator.GetEvent<AuthenticationExtendedEvent>().Publish(new UIMessage
            {
                Brief = Properties.Resources.AuthenticationExtended,
                Details = Properties.Resources.AuthenticationExtendedDetails
            });
        }

        private async Task WaitForInternetAccess()
        {
            Log.Verbose("Waiting for internet access...");
            _eventAggregator.GetEvent<AuthenticationExtendedEvent>().Publish(new UIMessage
            {
                Brief = Properties.Resources.WaitingInternetConnection,
                Details = Properties.Resources.WaitingInternetConnectionDetails
            });
            int attemptNumber = 0;
            var canDialOut = false;
            while (!canDialOut)
            {
                try
                {
                    attemptNumber++;
                    await _client.HttpClient.GetAsync("");
                    canDialOut = true;
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("The remote name could not be resolved"))
                {
                    if (attemptNumber % 4 == 0)
                        Log.Verbose("Still waiting for internet access...");
                }
            }
        }

        #endregion Methods
    }
}