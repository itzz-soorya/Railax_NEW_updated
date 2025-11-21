using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UserModule.Models
{
    public class BookingType
    {
        public string Type { get; set; } = "";
        public decimal Amount { get; set; }
    }

    // API Response Model
    public class HallTypesResponse
    {
        [JsonProperty("types")]
        public List<BookingType> Types { get; set; } = new List<BookingType>();
        
        [JsonProperty("advance_payment_enabled")]
        public bool AdvancePaymentEnabled { get; set; }
        
        [JsonProperty("default_advance_percentage")]
        public decimal DefaultAdvancePercentage { get; set; }
        
        [JsonProperty("hall_name")]
        public string? HallName { get; set; }
    }

    // Database Model for Settings
    public class Settings
    {
        public int Id { get; set; }
        public string AdminId { get; set; } = "";
        public string? Type1 { get; set; }
        public decimal? Type1Amount { get; set; }
        public string? Type2 { get; set; }
        public decimal? Type2Amount { get; set; }
        public string? Type3 { get; set; }
        public decimal? Type3Amount { get; set; }
        public string? Type4 { get; set; }
        public decimal? Type4Amount { get; set; }
        public bool AdvancePaymentEnabled { get; set; }
    public decimal DefaultAdvancePercentage { get; set; }
        public DateTime LastSynced { get; set; }
    }
}
