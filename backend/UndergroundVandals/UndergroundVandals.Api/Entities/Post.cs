namespace UndergroundVandals.Api.Entities;

public class Post
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "Graffiti";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public bool IsArchived { get; set; } = false;
    public List<string> Hashtags { get; set; } = new();
    public List<MediaAsset> MediaAssets { get; set; } = new();
}