using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.IoTManagement
{
    public interface IIoTManagementService
    {
        #region Methods

        void CreateThing(string serialNumber);

        void EnsureThingExists();

        bool ThingExists(string serialNumber);

        #endregion Methods
    }
}