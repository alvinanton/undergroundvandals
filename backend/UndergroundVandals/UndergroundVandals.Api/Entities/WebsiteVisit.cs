namespace UndergroundVandals.Api.Entities
{
    public class WebsiteVisit
    {
        public long Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public string CountryCode { get; set; } = "XX";
        public string? Region { get; set; }
        public string? City { get; set; }
        public string UserHash { get; set; } = string.Empty;
        public string DeviceType { get; set; } = "Unknown";
        public string? Referrer { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
