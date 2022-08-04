using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.Email
{
    public interface IEmailService
    {
        #region Methods

        void SendEmailTodaysLogs();

        #endregion Methods
    }
}