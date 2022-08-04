using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Security
{
    public class EncryptionService : IEncryptionService
    {
        #region Properties

        private byte[] PublicKeyBytes => Encoding.UTF8.GetBytes(PublicKey);

        private byte[] SecretKeyBytes => Encoding.UTF8.GetBytes(SecretKey);

        #endregion Properties

        #region Fields

        private const string PublicKey = "aPO0j88^";

        private const string SecretKey = "m8s#Hr1j";

        #endregion Fields

        #region Methods

        public string Decrypt(string encryptedPayload)
        {
            try
            {
                var decryptedPayload = string.Empty;

                // Replace spaces with '+' in the payload
                encryptedPayload = encryptedPayload.Replace(" ", "+");

                var payloadBytes = new byte[encryptedPayload.Length];
                payloadBytes = Convert.FromBase64String(encryptedPayload);

                using (var memoryStream = new MemoryStream())
                using (var desProvider = new DESCryptoServiceProvider())
                using (var cryptoStream = new CryptoStream(memoryStream, desProvider.CreateDecryptor(PublicKeyBytes, SecretKeyBytes), CryptoStreamMode.Write))
                {
                    cryptoStream.Write(payloadBytes, 0, payloadBytes.Length);
                    cryptoStream.FlushFinalBlock();
                    decryptedPayload = Encoding.UTF8.GetString(memoryStream.ToArray());
                }
                return decryptedPayload;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to decrypt payload");
                throw ex;
            }
        }

        public string Encrypt(string unencryptedPayload)
        {
            try
            {
                var encryptedPayload = string.Empty;
                var payloadBytes = Encoding.UTF8.GetBytes(unencryptedPayload);

                using (var memoryStream = new MemoryStream())
                using (var desProvider = new DESCryptoServiceProvider())
                using (var cryptoStream = new CryptoStream(memoryStream, desProvider.CreateEncryptor(PublicKeyBytes, SecretKeyBytes), CryptoStreamMode.Write))
                {
                    cryptoStream.Write(payloadBytes, 0, payloadBytes.Length);
                    cryptoStream.FlushFinalBlock();
                    encryptedPayload = Convert.ToBase64String(memoryStream.ToArray());
                }
                return encryptedPayload;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to encrypt payload");
                throw ex;
            }
        }

        #endregion Methods
    }
}