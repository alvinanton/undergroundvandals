namespace UndergroundVandals.Api.DTOs;

public sealed record SitePageSettingResponseDto(
    string PageKey,
    string Status,
    string Title,
    string Message,
    string AnnouncementTitle,
    string AnnouncementMessage,
    string CtaLabel,
    string CtaUrl,
    DateTime UpdatedAt);

public sealed record UpdateSitePageSettingDto(
    string? Status,
    string? Title,
    string? Message,
    string? AnnouncementTitle,
    string? AnnouncementMessage,
    string? CtaLabel,
    string? CtaUrl);
