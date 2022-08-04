using Amazon;
using Amazon.IoT;
using Amazon.IoT.Model;
using PursuitAlert.Client.Properties;
using PursuitAlert.Client.Services.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.IoTManagement
{
    public class IoTManagementService : IIoTManagementService
    {
        #region Fields

        private readonly string _awsKeyId;

        private readonly string _awsSecretKey;

        private readonly AmazonIoTClient _client;

        private readonly IEncryptionService _encryptionService;

        #endregion Fields

        #region Constructors

        public IoTManagementService(IEncryptionService encryptionService, string encryptedAWSKeyId, string encryptedAWSSecretKey)
        {
            _encryptionService = encryptionService;
            _awsKeyId = _encryptionService.Decrypt(encryptedAWSKeyId);
            _awsSecretKey = _encryptionService.Decrypt(encryptedAWSSecretKey);

            _client = new AmazonIoTClient(_awsKeyId, _awsSecretKey, RegionEndpoint.USEast1);
        }

        #endregion Constructors

        #region Methods

        public void CreateThing(string serialNumber)
        {
            _client.CreateThing(new CreateThingRequest
            {
                ThingName = serialNumber,
                ThingTypeName = Settings.Default.Stage == "dev" ? "Development" : Settings.Default.Stage
            });
        }

        public void EnsureThingExists()
        {
            var thingExists = ThingExists(Settings.Default.DeviceSerialNumber);
            if (!thingExists)
                CreateThing(Settings.Default.DeviceSerialNumber);
        }

        public bool ThingExists(string serialNumber)
        {
            try
            {
                var response = _client.DescribeThing(serialNumber);
                return !string.IsNullOrWhiteSpace(response.ThingId);
            }
            catch (ResourceNotFoundException)
            {
                return false;
            }
        }

        #endregion Methods
    }
}