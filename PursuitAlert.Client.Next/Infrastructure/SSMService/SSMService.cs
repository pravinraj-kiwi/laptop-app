using Amazon;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using PursuitAlert.Client.Services.Security;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.SSMService
{
    public class SSMService : ISSMService
    {
        #region Fields

        private readonly string _awsKeyId;

        private readonly string _awsSecretKey;

        private readonly AmazonSimpleSystemsManagementClient _client;

        private readonly IEncryptionService _encryptionService;

        #endregion Fields

        #region Constructors

        public SSMService(IEncryptionService encryptionService,
            string encryptedAWSKeyId,
            string encyptedAWSSecretKey)
        {
            _encryptionService = encryptionService;
            _awsKeyId = _encryptionService.Decrypt(encryptedAWSKeyId);
            _awsSecretKey = _encryptionService.Decrypt(encyptedAWSSecretKey);

            _client = new AmazonSimpleSystemsManagementClient(_awsKeyId, _awsSecretKey, RegionEndpoint.USEast1);
        }

        #endregion Constructors

        #region Methods

        public async Task<Parameter> GetParameter(string parameterName, bool withDecryption = true)
        {
            var request = new GetParameterRequest
            {
                Name = parameterName,
                WithDecryption = withDecryption
            };
            try
            {
                // ! Make sure to use ConfigureAwait. AWS calls never return without it
                var response = await _client.GetParameterAsync(request).ConfigureAwait(false);
                return response.Parameter;
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Failed to get SSM parameter: {paramterName}", parameterName);
                return null;
            }
        }

        public async Task<List<Parameter>> GetParameters(List<string> parameterNames, bool withDecryption = true)
        {
            var request = new GetParametersRequest
            {
                Names = parameterNames,
                WithDecryption = withDecryption
            };
            try
            {
                // ! Make sure to use ConfigureAwait. AWS calls never return without it
                var response = await _client.GetParametersAsync(request).ConfigureAwait(false);
                return response.Parameters;
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Failed to get SSM parameters: {paramterNames}", string.Join(", ", parameterNames).Trim().TrimEnd(','));
                return null;
            }
        }

        #endregion Methods
    }
}