using System.ComponentModel.DataAnnotations;
using UndergroundVandals.Api.DTOs;

public class AddAssetsDto
{
    [Required]
    public List<MediaAssetInputDto> Assets { get; set; } = new();
}