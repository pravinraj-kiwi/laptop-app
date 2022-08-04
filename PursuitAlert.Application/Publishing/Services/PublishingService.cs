using Amazon;
using Amazon.IoT;
using Amazon.IoT.Model;
using Amazon.IotData;
using Amazon.IotData.Model;
using Amazon.Runtime;
using Newtonsoft.Json;
using Prism.Events;
using PursuitAlert.Domain.Application.Events;
using PursuitAlert.Domain.Publishing.Events;
using PursuitAlert.Domain.Publishing.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Application.Publishing.Services
{
    public class PublishingService : IPublishingService
    {
        #region Properties

        public bool IsConnected { get; private set; } = false;

        public bool IsConnecting { get; private set; } = false;

        #endregion Properties

        #region Fields

        private readonly IEventAggregator _eventAggregator;

        private AmazonIotDataClient _client;

        private string _deviceSerial;

        private string _stage;

        #endregion Fields

        #region Constructors

        public PublishingService(IEventAggregator eventAggreator)
        {
            _eventAggregator = eventAggreator;
            IsConnected = false;
            IsConnecting = false;

            // ! Added the '?' in _client?.Dispose() because this was throwing an NRE if a device
            // wasn't connected
            _eventAggregator.GetEvent<ApplicationExitEvent>().Subscribe(exitCode => _client?.Dispose());
        }

        #endregion Constructors

        #region Methods

        public async Task SendLogMessage(string message, int Qos = 1) => await Publish($"pa/data/{_stage}/{_deviceSerial}/logs", message, Qos);

        public async Task SendTelemetry(string message, int Qos = 1) => await Publish($"pa/data/{_stage}/{_deviceSerial}/telemetry", message, Qos);

        public async Task SetCredentials(string accessKeyId, string secretAccessKey, string brokerUrl, string serialNumber, string stage)
        {
            IsConnecting = true;
            _deviceSerial = serialNumber;
            _stage = stage;
            _client = new AmazonIotDataClient(accessKeyId, secretAccessKey, new AmazonIotDataConfig
            {
                RegionEndpoint = RegionEndpoint.USEast1,
                ServiceURL = brokerUrl,
                Timeout = TimeSpan.FromMinutes(1)
            });

            _client.AfterResponseEvent += LogResponse;
            _client.ExceptionEvent += HandleSendFailure;

            try
            {
                await EnsureDeviceIsRegistered(accessKeyId, secretAccessKey, serialNumber, stage);
                _eventAggregator.GetEvent<BrokerInitialConnectEvent>().Publish();
                _eventAggregator.GetEvent<BrokerConnectedEvent>().Publish(brokerUrl);
                IsConnected = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to connect to broker at {brokerUrl}", brokerUrl);
                _eventAggregator.GetEvent<BrokerDisconnectedEvent>().Publish(brokerUrl);
                IsConnected = false;
            }
        }

        private async Task EnsureDeviceIsRegistered(string accessKeyId, string secretAccessKey, string serialNumber, string stage)
        {
            // TODO: Make sure a thing with this serial number exists
            var thingClient = new AmazonIoTClient(accessKeyId, secretAccessKey, RegionEndpoint.USEast1);
            try
            {
                await thingClient.DescribeThingAsync(serialNumber);
            }
            catch (Amazon.IoT.Model.ResourceNotFoundException)
            {
                // The thing doesn't exist, create it
                await thingClient.CreateThingAsync(new CreateThingRequest
                {
                    ThingName = serialNumber,
                    ThingTypeName = stage == "dev" ? "Development" : stage
                });
            }
        }

        private void HandleSendFailure(object sender, ExceptionEventArgs e)
        {
            var err = e as WebServiceExceptionEventArgs;
            Log.Error("Failed to send message: {exception}", err.Exception.Message);
        }

        private void LogResponse(object sender, ResponseEventArgs e)
        {
            var response = e as WebServiceResponseEventArgs;
            Log.Debug("Server response: {statusCode}", response.Response.HttpStatusCode);
        }

        private async Task Publish(string topic, string message, int Qos = 1)
        {
            var publishRequest = new PublishRequest
            {
                Payload = new MemoryStream(Encoding.UTF8.GetBytes(message)),
                Qos = Qos,
                Topic = topic
            };

            try
            {
                var response = await _client.PublishAsync(publishRequest);
                Log.Debug("Publish response: {response}", JsonConvert.SerializeObject(response));
                _eventAggregator.GetEvent<OnlineEvent>().Publish();
                _eventAggregator.GetEvent<BrokerConnectedEvent>().Publish(string.Empty);
            }
            catch (AmazonServiceException ex) when (ex.Message.Contains("NameResolutionFailure"))
            {
                Log.Debug("Failed to send message: PC is offline");
                _eventAggregator.GetEvent<MessagePublishFailureEvent>().Publish(new MessagePublishFailureEventArgs
                {
                    Exception = ex,
                    FailureReason = ex.Message,
                    Message = ex.Message
                });
                _eventAggregator.GetEvent<OfflineEvent>().Publish();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send message");
                _eventAggregator.GetEvent<MessagePublishFailureEvent>().Publish(new MessagePublishFailureEventArgs
                {
                    Exception = ex,
                    FailureReason = ex.Message,
                    Message = ex.Message
                });
            }
        }

        #endregion Methods
    }
}