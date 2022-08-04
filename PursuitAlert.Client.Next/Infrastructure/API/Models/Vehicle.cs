using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.API.Models
{
    public class Vehicle
    {
        #region Properties

        public string Bearing { get; set; }

        [JsonProperty("current_status")]
        public string CurrentStatus { get; set; }

        [JsonProperty("hardware_id")]
        public string HardwareId { get; set; }

        public int Id { get; set; }

        [JsonProperty("jurisdiction_id")]
        public int JurisdictionId { get; set; }

        [JsonProperty("k9_officer")]
        public string K9Officer { get; set; }

        [JsonProperty("last_update")]
        public DateTime? LastUpdate { get; set; }

        public string Name { get; set; }

        public string Notes { get; set; }

        public string Officer { get; set; }

        [JsonProperty("secondary_officer")]
        public string SecondaryOfficer { get; set; }

        public string Speed { get; set; }

        [JsonProperty("unit_id")]
        public string UnitId { get; set; }

        [JsonProperty("vehicle_model")]
        public string VehicleModel { get; set; }

        #endregion Properties
    }
}