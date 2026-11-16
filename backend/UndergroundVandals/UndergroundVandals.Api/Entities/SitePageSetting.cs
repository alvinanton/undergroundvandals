namespace UndergroundVandals.Api.Entities;

public class SitePageSetting
{
    public string PageKey { get; set; } = string.Empty;
    public string Status { get; set; } = "Live";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AnnouncementTitle { get; set; } = string.Empty;
    public string AnnouncementMessage { get; set; } = string.Empty;
    public string CtaLabel { get; set; } = string.Empty;
    public string CtaUrl { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
