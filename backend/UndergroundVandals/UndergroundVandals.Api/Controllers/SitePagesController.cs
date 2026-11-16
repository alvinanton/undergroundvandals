using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.DTOs;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.Controllers;

[ApiController]
[Route("api/site-pages")]
[Tags("Site pages")]
public sealed class SitePagesController(AppDbContext context) : ControllerBase
{
    private static readonly HashSet<string> AllowedPages = new(StringComparer.OrdinalIgnoreCase)
        { "home", "archive", "gallery", "shop" };
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "Live", "ComingSoon", "Disabled" };

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SitePageSettingResponseDto>>> GetPublic(CancellationToken cancellationToken)
    {
        var pages = await context.SitePageSettings.AsNoTracking()
            .OrderBy(page => page.PageKey)
            .ToListAsync(cancellationToken);
        return Ok(pages.Select(Map).ToList());
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<SitePageSettingResponseDto>>> GetAdmin(CancellationToken cancellationToken)
    {
        var pages = await context.SitePageSettings.AsNoTracking()
            .OrderBy(page => page.PageKey)
            .ToListAsync(cancellationToken);
        return Ok(pages.Select(Map).ToList());
    }

    [HttpPut("{pageKey}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SitePageSettingResponseDto>> Update(
        string pageKey,
        [FromBody] UpdateSitePageSettingDto request,
        CancellationToken cancellationToken)
    {
        if (!AllowedPages.Contains(pageKey)) return NotFound();
        var status = request.Status?.Trim() ?? string.Empty;
        var title = request.Title?.Trim() ?? string.Empty;
        var message = request.Message?.Trim() ?? string.Empty;
        var announcementTitle = request.AnnouncementTitle?.Trim() ?? string.Empty;
        var announcementMessage = request.AnnouncementMessage?.Trim() ?? string.Empty;
        var ctaLabel = request.CtaLabel?.Trim() ?? string.Empty;
        var ctaUrl = request.CtaUrl?.Trim() ?? string.Empty;

        if (!AllowedStatuses.Contains(status))
            return BadRequest(new { message = "Status must be Live, ComingSoon, or Disabled." });
        if (title.Length == 0 || title.Length > 120 || message.Length > 1000 ||
            announcementTitle.Length > 120 || announcementMessage.Length > 1000 ||
            ctaLabel.Length > 60 || ctaUrl.Length > 500)
            return BadRequest(new { message = "One or more page fields exceed the allowed length." });
        if (!string.IsNullOrWhiteSpace(ctaUrl) &&
            (!ctaUrl.StartsWith('/') || ctaUrl.StartsWith("//", StringComparison.Ordinal)))
            return BadRequest(new { message = "CTA links must be local paths beginning with a single slash." });

        var page = await context.SitePageSettings.FirstOrDefaultAsync(
            item => item.PageKey == pageKey.ToLowerInvariant(), cancellationToken);
        if (page is null) return NotFound(new { message = "Page configuration was not found." });

        page.Status = AllowedStatuses.First(allowed => allowed.Equals(status, StringComparison.OrdinalIgnoreCase));
        page.Title = title;
        page.Message = message;
        page.AnnouncementTitle = announcementTitle;
        page.AnnouncementMessage = announcementMessage;
        page.CtaLabel = ctaLabel;
        page.CtaUrl = ctaUrl;
        page.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return Ok(Map(page));
    }

    private static SitePageSettingResponseDto Map(SitePageSetting page) => new(
        page.PageKey, page.Status, page.Title, page.Message, page.AnnouncementTitle,
        page.AnnouncementMessage, page.CtaLabel, page.CtaUrl, page.UpdatedAt);
}
