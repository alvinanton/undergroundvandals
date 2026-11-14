namespace UndergroundVandals.Api.Entities;

public class GalleryArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Category { get; set; } = "EDITORIAL";
    public string HeroImageUrl { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public string VideoSource { get; set; } = "NONE";
    public string ContentMarkdown { get; set; } = string.Empty;
    public int ReadingTimeMinutes { get; set; } = 5;
    public string? SubjectName { get; set; }
    public string? Location { get; set; }
    public string? ExternalUrl { get; set; }
    public DateTime? EventDate { get; set; }
    public bool IsFeatured { get; set; } = false;
    public bool IsPublished { get; set; } = true;
    public string? ShopCollectionSlug { get; set; }
    public string? ShopCollectionTitle { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> GalleryImageUrls { get; set; } = new();
}