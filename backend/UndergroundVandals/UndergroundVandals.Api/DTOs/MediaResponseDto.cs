using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.DTOs;

public class MediaAssetDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class MediaResponseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> Hashtags { get; set; } = new();
    public List<MediaAssetDto> Media { get; set; } = new();
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
}