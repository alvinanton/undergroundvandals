namespace UndergroundVandals.Api.DTOs;

/// <summary>Message returned by a controller, distinct from automatic validation errors.</summary>
public record ApiMessageResponseDto(string Message);

/// <summary>Result of toggling a publication's archived state.</summary>
public record ArchiveStatusResponseDto(string Message, Guid Id, bool IsArchived);

/// <summary>Result of toggling a gallery article's featured state.</summary>
public record FeaturedStatusResponseDto(string Message, Guid Id, bool IsFeatured);
