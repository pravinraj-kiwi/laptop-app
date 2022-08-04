using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Telemetry
{
    public static class DoubleExtensions
    {
        #region Methods

        public static bool IsSerializable(this double number) => !double.IsNaN(number) && !double.IsInfinity(number);

        #endregion Methods
    }
}