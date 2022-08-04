using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.Security.Services
{
    public interface IEncryptionService
    {
        #region Methods

        string Decrypt(string encryptedPayload);

        string Encrypt(string unencryptedPayload);

        #endregion Methods
    }
}