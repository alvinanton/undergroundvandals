namespace UndergroundVandals.Api.Entities;

public enum MediaType
{
    Photo,
    Video
}

public class MediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; } = false;
    public List<string> Hashtags { get; set; } = new();
    public List<MediaAsset> MediaAssets { get; set; } = new();
}