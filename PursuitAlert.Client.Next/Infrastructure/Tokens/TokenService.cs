using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using PursuitAlert.Client.Infrastructure.SSMService;
using PursuitAlert.Client.Services.Security;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.Tokens
{
    public class TokenService : ITokenService
    {
        #region Fields

        private const string AuthenticationEndpointKey = "authentication-endpoint";

        private const string ServiceAccountClientIdKey = "windows-app-service-account-client-id";

        private const string ServiceAccountPasswordKey = "windows-app-service-account-password";

        private const string ServiceAccountUsernameKey = "windows-app-service-account-username";

        private readonly string _awsKeyId;

        private readonly string _awsSecretKey;

        private readonly AmazonCognitoIdentityProviderClient _cognito;

        private readonly IEncryptionService _encryptionService;

        private readonly ISSMService _ssmService;

        private string _authenticationEndpoint;

        private string _clientId;

        private string _password;

        private string _username;

        #endregion Fields

        #region Constructors

        public TokenService(ISSMService ssmService,
            IEncryptionService encryptionService,
            string encryptedAWSKeyId,
            string encryptedAWSSecretKey)
        {
            _ssmService = ssmService;
            _encryptionService = encryptionService;
            _awsKeyId = _encryptionService.Decrypt(encryptedAWSKeyId);
            _awsSecretKey = _encryptionService.Decrypt(encryptedAWSSecretKey);

            Log.Debug("Authenticating with Cognito");
            _cognito = new AmazonCognitoIdentityProviderClient(_awsKeyId, _awsSecretKey, RegionEndpoint.USEast1);
            Log.Verbose("Authenticated with Cognito");
        }

        #endregion Constructors

        #region Methods

        public async Task<string> GetServiceAccountToken()
        {
            // ! Always include ConfigureAwait; the calls won't return without them
            Log.Debug("Getting service account paramters from SSM");
            var parameters = await _ssmService.GetParameters(new List<string>
            {
                ServiceAccountUsernameKey,
                ServiceAccountPasswordKey,
                AuthenticationEndpointKey,
                ServiceAccountClientIdKey
            },
            true)
                .ConfigureAwait(false);

            Log.Verbose("{paramterCount} parameter(s) retrieved from SSM successfully", parameters == null ? 0 : parameters.Count);

            var username = parameters.First(p => p.Name.Equals(ServiceAccountUsernameKey));
            var password = parameters.First(p => p.Name.Equals(ServiceAccountPasswordKey));
            var clientId = parameters.First(p => p.Name.Equals(ServiceAccountClientIdKey));
            var authenticationEndpoint = parameters.First(p => p.Name.Equals(AuthenticationEndpointKey));
            _username = username.Value;
            _password = password.Value;
            _clientId = clientId.Value;
            _authenticationEndpoint = authenticationEndpoint.Value;

            Log.Debug("Getting ID token from Cognito for the service account retrieved from SSM");
            try
            {
                var authResponse = await _cognito.InitiateAuthAsync(new InitiateAuthRequest
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

                return idToken;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to get an ID token from Cognito for the service account retrieved from SSM");
                return string.Empty;
            }
        }

        #endregion Methods
    }
}