//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PursuitAlert.Client.Old.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "16.7.0.0")]
    internal sealed partial class MqttSettings : global::System.Configuration.ApplicationSettingsBase {
        
        private static MqttSettings defaultInstance = ((MqttSettings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new MqttSettings())));
        
        public static MqttSettings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string MqttEndpoint {
            get {
                return ((string)(this["MqttEndpoint"]));
            }
            set {
                this["MqttEndpoint"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string Stack {
            get {
                return ((string)(this["Stack"]));
            }
            set {
                this["Stack"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string ThingName {
            get {
                return ((string)(this["ThingName"]));
            }
            set {
                this["ThingName"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.SpecialSettingAttribute(global::System.Configuration.SpecialSetting.WebServiceUrl)]
        [global::System.Configuration.DefaultSettingValueAttribute("https://bb2.auth.pursuitalert.net/v1/authenticateVehicle/")]
        public string AuthenticationUrl {
            get {
                return ((string)(this["AuthenticationUrl"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("certificates")]
        public string CertificatesDirectory {
            get {
                return ((string)(this["CertificatesDirectory"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.SpecialSettingAttribute(global::System.Configuration.SpecialSetting.WebServiceUrl)]
        [global::System.Configuration.DefaultSettingValueAttribute("https://www.websecurity.digicert.com/content/dam/websitesecurity/digitalassets/de" +
            "sktop/pdfs/roots/VeriSign-Class%203-Public-Primary-Certification-Authority-G5.pe" +
            "m")]
        public string RootCertificateUrl {
            get {
                return ((string)(this["RootCertificateUrl"]));
            }
            set {
                this["RootCertificateUrl"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool DownloadRootCertificate {
            get {
                return ((bool)(this["DownloadRootCertificate"]));
            }
            set {
                this["DownloadRootCertificate"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("8883")]
        public int PortNumber {
            get {
                return ((int)(this["PortNumber"]));
            }
            set {
                this["PortNumber"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool UseATSEndpoint {
            get {
                return ((bool)(this["UseATSEndpoint"]));
            }
        }
    }
}
