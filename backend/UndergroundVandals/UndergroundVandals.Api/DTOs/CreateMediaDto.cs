using System.ComponentModel.DataAnnotations;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.DTOs;

public class CreateMediaDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public string Category { get; set; } = "General";
    
    public List<string>? Hashtags { get; set; }

    [Required]
    public List<MediaAssetInputDto> Assets { get; set; } = new();
}

public class MediaAssetInputDto
{
    [Required]
    public string Url { get; set; } = string.Empty;

    [Required]
    public string PublicId { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = "image"; // "image" or "video"
}