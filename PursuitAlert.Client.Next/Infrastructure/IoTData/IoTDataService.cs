using Amazon;
using Amazon.IotData;
using Amazon.IotData.Model;
using Amazon.Runtime;
using Newtonsoft.Json;
using PursuitAlert.Client.Infrastructure.SSMService;
using PursuitAlert.Client.Services.Security;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.IoTData
{
    public class IoTDataService : IIoTDataService
    {
        #region Fields

        private const string BrokerUrlKey = "iot-data-endpoint-dev";

        private readonly string _awsKeyId;

        private readonly string _awsSecretKey;

        private readonly ISSMService _ssmService;

        private AmazonIotDataClient _client;

        #endregion Fields

        #region Constructors

        public IoTDataService(IEncryptionService encryptionService,
            ISSMService ssmService,
            string encryptedAWSKeyId,
            string encryptedAWSSecretKey)
        {
            _awsKeyId = encryptionService.Decrypt(encryptedAWSKeyId);
            _awsSecretKey = encryptionService.Decrypt(encryptedAWSSecretKey);
            _ssmService = ssmService;
        }

        #endregion Constructors

        #region Methods

        public async Task Authenticate()
        {
            var brokerUrl = await _ssmService.GetParameter(BrokerUrlKey).ConfigureAwait(false);

            _client = new AmazonIotDataClient(_awsKeyId, _awsSecretKey, new AmazonIotDataConfig
            {
                RegionEndpoint = RegionEndpoint.USEast1,
                ServiceURL = $"https://{brokerUrl.Value}",
                Timeout = TimeSpan.FromMinutes(1)
            });
        }

        public void Send(string topic, string message, int Qos = 1)
        {
            Log.Verbose("Publishing message to topic {topic}: {message}", topic, message);
            var publishRequest = new PublishRequest
            {
                Payload = new MemoryStream(Encoding.UTF8.GetBytes(message)),
                Qos = Qos,
                Topic = topic
            };

            var response = _client.Publish(publishRequest);
            Log.Verbose("Publish response: {response}", JsonConvert.SerializeObject(response));
        }

        public async Task SendAsync(string topic, string message, int Qos = 1)
        {
            Log.Verbose("Publishing message to topic {topic}: {message}", topic, message);
            var publishRequest = new PublishRequest
            {
                Payload = new MemoryStream(Encoding.UTF8.GetBytes(message)),
                Qos = Qos,
                Topic = topic
            };

            var response = await _client.PublishAsync(publishRequest);
            Log.Verbose("Publish response: {response}", JsonConvert.SerializeObject(response));
        }

        #endregion Methods
    }
}