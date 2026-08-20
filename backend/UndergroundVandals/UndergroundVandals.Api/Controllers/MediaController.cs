using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.DTOs;
using UndergroundVandals.Api.Entities;
using UndergroundVandals.Api.Services;

namespace UndergroundVandals.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public MediaController(AppDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MediaResponseDto>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? tag,
        [FromQuery] bool includeArchived = false)
    {
        var query = _context.MediaItems.AsQueryable();

        if (!includeArchived)
            query = query.Where(m => !m.IsArchived);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(m => m.Category.ToLower() == category.ToLower());

        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(m => m.Hashtags.Contains(tag.ToLower()));

        var items = await query
            .Include(m => m.MediaAssets)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MediaResponseDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Category = m.Category,
                Hashtags = m.Hashtags,
                IsArchived = m.IsArchived,
                CreatedAt = m.CreatedAt,
                Media = m.MediaAssets.Select(a => new MediaAssetDto
                {
                    Id = a.Id,
                    Url = a.Url,
                    Type = a.Type == MediaType.Photo ? "image" : "video"
                }).ToList()
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MediaResponseDto>> GetById(Guid id)
    {
        var item = await _context.MediaItems
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        return Ok(new MediaResponseDto
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            Category = item.Category,
            Hashtags = item.Hashtags,
            IsArchived = item.IsArchived,
            CreatedAt = item.CreatedAt,
            Media = item.MediaAssets.Select(a => new MediaAssetDto
            {
                Id = a.Id,
                Url = a.Url,
                Type = a.Type == MediaType.Photo ? "image" : "video"
            }).ToList()
        });
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPost("upload")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
    public async Task<ActionResult<MediaResponseDto>> Upload([FromForm] CreateMediaDto dto)
    {
        if (dto.Files == null || !dto.Files.Any())
            return BadRequest(new { message = "At least one file is required." });

        var mediaItem = new MediaItem
        {
            Title = dto.Title,
            Description = dto.Description,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category,
            Hashtags = dto.Hashtags ?? new List<string>()
        };

        foreach (var file in dto.Files)
        {
            var isVideo = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
            var uploadResult = isVideo
                ? await _fileStorageService.UploadVideoAsync(file)
                : await _fileStorageService.UploadImageAsync(file);

            if (!uploadResult.Success)
                return BadRequest(new { message = uploadResult.Error });

            mediaItem.MediaAssets.Add(new MediaAsset
            {
                Url = uploadResult.Url,
                PublicId = uploadResult.PublicId,
                Type = isVideo ? MediaType.Video : MediaType.Photo
            });
        }

        _context.MediaItems.Add(mediaItem);
        await _context.SaveChangesAsync();

        var response = new MediaResponseDto
        {
            Id = mediaItem.Id,
            Title = mediaItem.Title,
            Description = mediaItem.Description,
            Category = mediaItem.Category,
            Hashtags = mediaItem.Hashtags,
            IsArchived = mediaItem.IsArchived,
            CreatedAt = mediaItem.CreatedAt,
            Media = mediaItem.MediaAssets.Select(a => new MediaAssetDto
            {
                Id = a.Id,
                Url = a.Url,
                Type = a.Type == MediaType.Photo ? "image" : "video"
            }).ToList()
        };

        return CreatedAtAction(nameof(GetById), new { id = mediaItem.Id }, response);
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPost("{id:guid}/assets")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
    public async Task<ActionResult<MediaResponseDto>> AddAssets(Guid id, [FromForm] List<IFormFile> files)
    {
        if (files == null || !files.Any())
            return BadRequest(new { message = "At least one file is required." });

        var item = await _context.MediaItems
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        foreach (var file in files)
        {
            var isVideo = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
            var uploadResult = isVideo
                ? await _fileStorageService.UploadVideoAsync(file)
                : await _fileStorageService.UploadImageAsync(file);

            if (!uploadResult.Success)
                return BadRequest(new { message = uploadResult.Error });

            item.MediaAssets.Add(new MediaAsset
            {
                Url = uploadResult.Url,
                PublicId = uploadResult.PublicId,
                Type = isVideo ? MediaType.Video : MediaType.Photo
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new MediaResponseDto
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            Category = item.Category,
            Hashtags = item.Hashtags,
            IsArchived = item.IsArchived,
            CreatedAt = item.CreatedAt,
            Media = item.MediaAssets.Select(a => new MediaAssetDto
            {
                Id = a.Id,
                Url = a.Url,
                Type = a.Type == MediaType.Photo ? "image" : "video"
            }).ToList()
        });
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpDelete("assets/{assetId:guid}")]
    public async Task<IActionResult> DeleteAsset(Guid assetId)
    {
        var asset = await _context.MediaAssets.FindAsync(assetId);

        if (asset == null)
            return NotFound(new { message = "Asset not found." });

        if (!string.IsNullOrWhiteSpace(asset.PublicId))
        {
            await _fileStorageService.DeleteFileAsync(asset.PublicId);
        }

        _context.MediaAssets.Remove(asset);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPatch("{id:guid}/archive")]
    public async Task<IActionResult> ToggleArchive(Guid id)
    {
        var item = await _context.MediaItems.FindAsync(id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        item.IsArchived = !item.IsArchived;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = item.IsArchived ? "Media item successfully archived." : "Media item successfully unarchived.",
            id = item.Id,
            isArchived = item.IsArchived
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.MediaItems
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        foreach (var asset in item.MediaAssets)
        {
            await _fileStorageService.DeleteFileAsync(asset.PublicId);
        }

        _context.MediaItems.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MediaResponseDto>> Update(Guid id, [FromBody] UpdateMediaDto dto)
    {
        var item = await _context.MediaItems
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        item.Title = dto.Title;
        item.Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category;
        item.Description = dto.Description;
        item.Hashtags = dto.Hashtags ?? new List<string>();

        await _context.SaveChangesAsync();

        return Ok(new MediaResponseDto
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            Category = item.Category,
            Hashtags = item.Hashtags,
            IsArchived = item.IsArchived,
            CreatedAt = item.CreatedAt,
            Media = item.MediaAssets.Select(a => new MediaAssetDto
            {
                Id = a.Id,
                Url = a.Url,
                Type = a.Type == MediaType.Photo ? "image" : "video"
            }).ToList()
        });
    }
}