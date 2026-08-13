namespace UndergroundVandals.Api.DTOs;

public class FileUploadResult
{
    public string Url { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}