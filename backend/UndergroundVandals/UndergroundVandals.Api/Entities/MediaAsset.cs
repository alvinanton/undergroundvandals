namespace UndergroundVandals.Api.Entities;

public class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public MediaType Type { get; set; }
    public Guid MediaItemId { get; set; }
    public MediaItem MediaItem { get; set; } = null!;
}