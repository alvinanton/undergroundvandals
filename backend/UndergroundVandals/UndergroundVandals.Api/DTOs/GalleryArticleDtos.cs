namespace UndergroundVandals.Api.DTOs;

public record CreateGalleryArticleDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Category { get; init; } = "EDITORIAL";

    public string HeroImageUrl { get; init; } = string.Empty;
    public string? VideoUrl { get; init; }
    public string VideoSource { get; init; } = "NONE";
    public string ContentMarkdown { get; init; } = string.Empty;
    public int ReadingTimeMinutes { get; init; } = 5;

    public string? SubjectName { get; init; }
    public string? Location { get; init; }
    public string? ExternalUrl { get; init; }
    public DateTime? EventDate { get; init; }

    public bool IsFeatured { get; init; } = false;
    public bool IsPublished { get; init; } = true;

    // Optional E-Shop Integration
    public string? ShopCollectionSlug { get; init; }
    public string? ShopCollectionTitle { get; init; }

    public List<string> Tags { get; init; } = new();
    public List<string> GalleryImageUrls { get; init; } = new();
}

public record UpdateGalleryArticleDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Category { get; init; } = "EDITORIAL";

    public string HeroImageUrl { get; init; } = string.Empty;
    public string? VideoUrl { get; init; }
    public string VideoSource { get; init; } = "NONE";
    public string ContentMarkdown { get; init; } = string.Empty;
    public int ReadingTimeMinutes { get; init; } = 5;

    public string? SubjectName { get; init; }
    public string? Location { get; init; }
    public string? ExternalUrl { get; init; }
    public DateTime? EventDate { get; init; }

    public bool IsFeatured { get; init; } = false;
    public bool IsPublished { get; init; } = true;

    // Optional E-Shop Integration
    public string? ShopCollectionSlug { get; init; }
    public string? ShopCollectionTitle { get; init; }

    public List<string> Tags { get; init; } = new();
    public List<string> GalleryImageUrls { get; init; } = new();
}

public record GalleryArticleResponseDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;

    public string HeroImageUrl { get; init; } = string.Empty;
    public string? VideoUrl { get; init; }
    public string VideoSource { get; init; } = "NONE";
    public string ContentMarkdown { get; init; } = string.Empty;
    public int ReadingTimeMinutes { get; init; }

    public string? SubjectName { get; init; }
    public string? Location { get; init; }
    public string? ExternalUrl { get; init; }
    public DateTime? EventDate { get; init; }

    public bool IsFeatured { get; init; }
    public bool IsPublished { get; init; }

    // Optional E-Shop Integration
    public string? ShopCollectionSlug { get; init; }
    public string? ShopCollectionTitle { get; init; }

    public DateTime PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public List<string> Tags { get; init; } = new();
    public List<string> GalleryImageUrls { get; init; } = new();
}