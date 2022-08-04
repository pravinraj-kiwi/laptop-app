using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Device.Utilities
{
    public static class ComPortNames
    {
        #region Fields

        private const string DeviceParamtersKey = "Device Parameters";

        private const string DeviceRegExKey = "^VID_{0}.PID_{1}";

        private const string Name = "PortName";

        private const string RegistryKeyNameKey = "SYSTEM\\CurrentControlSet\\Enum";

        private const string USBRegistryKey = "USB";

        #endregion Fields

        #region Methods

        public static List<string> GetComPortNamesForDevice(string VID, string PID)
        {
            var pattern = string.Format(DeviceRegExKey, VID, PID);
            var _rx = new Regex(pattern, RegexOptions.IgnoreCase);
            var comports = new List<string>();
            var rk1 = Registry.LocalMachine;
            var rk2 = rk1.OpenSubKey(RegistryKeyNameKey);
            var rk3 = rk2.OpenSubKey(USBRegistryKey);
            foreach (var s in rk3.GetSubKeyNames())
            {
                if (_rx.Match(s).Success)
                {
                    var rk4 = rk3.OpenSubKey(s);
                    foreach (var s2 in rk4.GetSubKeyNames())
                    {
                        var rk5 = rk4.OpenSubKey(s2);
                        var rk6 = rk5.OpenSubKey(DeviceParamtersKey);
                        comports.Add((string)rk6.GetValue(Name));
                    }
                }
            }
            return comports;
        }

        #endregion Methods
    }
}