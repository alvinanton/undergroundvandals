using System.ComponentModel.DataAnnotations;

namespace UndergroundVandals.Api.DTOs;

public class UpdateMediaDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = "General";

    public string? Description { get; set; }

    public List<string>? Hashtags { get; set; }
}