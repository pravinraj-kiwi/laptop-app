using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Security
{
    public interface IEncryptionService
    {
        #region Methods

        string Decrypt(string encryptedPayload);

        string Encrypt(string unencryptedPayload);

        #endregion Methods
    }
}