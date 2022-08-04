using Amazon.SimpleSystemsManagement.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.SSMService
{
    public interface ISSMService
    {
        #region Methods

        Task<Parameter> GetParameter(string parameterName, bool withEncryption = true);

        Task<List<Parameter>> GetParameters(List<string> parameterNames, bool withEncryption = true);

        #endregion Methods
    }
}