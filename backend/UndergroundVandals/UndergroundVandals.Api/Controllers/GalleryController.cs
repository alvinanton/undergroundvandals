using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.DTOs;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GalleryController : ControllerBase
{
    private readonly AppDbContext _context;

    public GalleryController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<GalleryArticleResponseDto>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? tag,
        [FromQuery] bool? isFeatured,
        [FromQuery] bool includeUnpublished = false)
    {
        var query = _context.GalleryArticles.AsQueryable();

        if (!includeUnpublished)
            query = query.Where(a => a.IsPublished);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(a => a.Category.ToLower() == category.ToLower());

        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(a => a.Tags.Contains(tag.ToLower()));

        if (isFeatured.HasValue)
            query = query.Where(a => a.IsFeatured == isFeatured.Value);

        var articles = await query
            .OrderByDescending(a => a.PublishedAt)
            .ToListAsync();

        return Ok(articles.Select(MapToDto).ToList());
    }

    [HttpGet("featured")]
    public async Task<ActionResult<GalleryArticleResponseDto>> GetFeatured()
    {
        var featured = await _context.GalleryArticles
            .Where(a => a.IsPublished && a.IsFeatured)
            .OrderByDescending(a => a.PublishedAt)
            .FirstOrDefaultAsync();

        if (featured == null)
        {
            featured = await _context.GalleryArticles
                .Where(a => a.IsPublished)
                .OrderByDescending(a => a.PublishedAt)
                .FirstOrDefaultAsync();
        }

        if (featured == null)
            return NotFound(new { message = "No gallery articles found." });

        return Ok(MapToDto(featured));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GalleryArticleResponseDto>> GetById(Guid id)
    {
        var article = await _context.GalleryArticles.FindAsync(id);

        if (article == null)
            return NotFound(new { message = "Gallery article not found." });

        return Ok(MapToDto(article));
    }

    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<GalleryArticleResponseDto>> GetBySlug(string slug)
    {
        var article = await _context.GalleryArticles
            .FirstOrDefaultAsync(a => a.Slug.ToLower() == slug.ToLower());

        if (article == null)
            return NotFound(new { message = "Gallery article not found." });

        return Ok(MapToDto(article));
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPost]
    public async Task<ActionResult<GalleryArticleResponseDto>> Create([FromBody] CreateGalleryArticleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Title is required." });

        var slug = string.IsNullOrWhiteSpace(dto.Slug)
            ? GenerateSlug(dto.Title)
            : dto.Slug.ToLower();

        if (await _context.GalleryArticles.AnyAsync(a => a.Slug == slug))
            return BadRequest(new { message = "An article with this slug already exists." });

        if (dto.IsFeatured)
        {
            await UnfeatureAllArticlesAsync();
        }

        var article = new GalleryArticle
        {
            Title = dto.Title,
            Slug = slug,
            Subtitle = dto.Subtitle,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "EDITORIAL" : dto.Category.ToUpper(),
            HeroImageUrl = dto.HeroImageUrl,
            VideoUrl = dto.VideoUrl,
            VideoSource = string.IsNullOrWhiteSpace(dto.VideoSource) ? "NONE" : dto.VideoSource.ToUpper(),
            ContentMarkdown = dto.ContentMarkdown,
            ReadingTimeMinutes = dto.ReadingTimeMinutes <= 0 ? 5 : dto.ReadingTimeMinutes,
            SubjectName = dto.SubjectName,
            Location = dto.Location,
            ExternalUrl = dto.ExternalUrl,
            EventDate = dto.EventDate,
            IsFeatured = dto.IsFeatured,
            IsPublished = dto.IsPublished,
            ShopCollectionSlug = dto.ShopCollectionSlug,
            ShopCollectionTitle = dto.ShopCollectionTitle,
            PublishedAt = DateTime.UtcNow,
            Tags = dto.Tags?.Select(t => t.ToLower()).ToList() ?? new List<string>(),
            GalleryImageUrls = dto.GalleryImageUrls ?? new List<string>()
        };

        _context.GalleryArticles.Add(article);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = article.Id }, MapToDto(article));
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GalleryArticleResponseDto>> Update(Guid id, [FromBody] UpdateGalleryArticleDto dto)
    {
        var article = await _context.GalleryArticles.FindAsync(id);

        if (article == null)
            return NotFound(new { message = "Gallery article not found." });

        var slug = string.IsNullOrWhiteSpace(dto.Slug)
            ? GenerateSlug(dto.Title)
            : dto.Slug.ToLower();

        if (article.Slug != slug && await _context.GalleryArticles.AnyAsync(a => a.Slug == slug))
            return BadRequest(new { message = "An article with this slug already exists." });

        if (dto.IsFeatured && !article.IsFeatured)
        {
            await UnfeatureAllArticlesAsync();
        }

        article.Title = dto.Title;
        article.Slug = slug;
        article.Subtitle = dto.Subtitle;
        article.Category = string.IsNullOrWhiteSpace(dto.Category) ? "EDITORIAL" : dto.Category.ToUpper();
        article.HeroImageUrl = dto.HeroImageUrl;
        article.VideoUrl = dto.VideoUrl;
        article.VideoSource = string.IsNullOrWhiteSpace(dto.VideoSource) ? "NONE" : dto.VideoSource.ToUpper();
        article.ContentMarkdown = dto.ContentMarkdown;
        article.ReadingTimeMinutes = dto.ReadingTimeMinutes <= 0 ? 5 : dto.ReadingTimeMinutes;
        article.SubjectName = dto.SubjectName;
        article.Location = dto.Location;
        article.ExternalUrl = dto.ExternalUrl;
        article.EventDate = dto.EventDate;
        article.IsFeatured = dto.IsFeatured;
        article.IsPublished = dto.IsPublished;
        article.ShopCollectionSlug = dto.ShopCollectionSlug;
        article.ShopCollectionTitle = dto.ShopCollectionTitle;
        article.UpdatedAt = DateTime.UtcNow;
        article.Tags = dto.Tags?.Select(t => t.ToLower()).ToList() ?? new List<string>();
        article.GalleryImageUrls = dto.GalleryImageUrls ?? new List<string>();

        await _context.SaveChangesAsync();

        return Ok(MapToDto(article));
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPatch("{id:guid}/featured")]
    public async Task<IActionResult> ToggleFeatured(Guid id)
    {
        var article = await _context.GalleryArticles.FindAsync(id);

        if (article == null)
            return NotFound(new { message = "Gallery article not found." });

        if (!article.IsFeatured)
        {
            await UnfeatureAllArticlesAsync();
            article.IsFeatured = true;
        }
        else
        {
            article.IsFeatured = false;
        }

        article.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = article.IsFeatured ? "Article marked as featured." : "Article unfeatured.",
            id = article.Id,
            isFeatured = article.IsFeatured
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var article = await _context.GalleryArticles.FindAsync(id);

        if (article == null)
            return NotFound(new { message = "Gallery article not found." });

        _context.GalleryArticles.Remove(article);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task UnfeatureAllArticlesAsync()
    {
        var currentFeatured = await _context.GalleryArticles
            .Where(a => a.IsFeatured)
            .ToListAsync();

        foreach (var item in currentFeatured)
        {
            item.IsFeatured = false;
        }
    }

    private static string GenerateSlug(string title)
    {
        return title.ToLower()
            .Replace(" ", "-")
            .Replace(":", "")
            .Replace("?", "")
            .Replace("!", "")
            .Replace("/", "");
    }

    private static GalleryArticleResponseDto MapToDto(GalleryArticle article)
    {
        return new GalleryArticleResponseDto
        {
            Id = article.Id,
            Title = article.Title,
            Slug = article.Slug,
            Subtitle = article.Subtitle,
            Category = article.Category,
            HeroImageUrl = article.HeroImageUrl,
            VideoUrl = article.VideoUrl,
            VideoSource = article.VideoSource,
            ContentMarkdown = article.ContentMarkdown,
            ReadingTimeMinutes = article.ReadingTimeMinutes,
            SubjectName = article.SubjectName,
            Location = article.Location,
            ExternalUrl = article.ExternalUrl,
            EventDate = article.EventDate,
            IsFeatured = article.IsFeatured,
            IsPublished = article.IsPublished,
            ShopCollectionSlug = article.ShopCollectionSlug,
            ShopCollectionTitle = article.ShopCollectionTitle,
            PublishedAt = article.PublishedAt,
            CreatedAt = article.CreatedAt,
            UpdatedAt = article.UpdatedAt,
            Tags = article.Tags,
            GalleryImageUrls = article.GalleryImageUrls
        };
    }
}