using System.ComponentModel.DataAnnotations;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.DTOs;

public class CreateMediaDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public MediaType Type { get; set; }

    public string Category { get; set; } = "General";
    public List<string>? Hashtags { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;
}