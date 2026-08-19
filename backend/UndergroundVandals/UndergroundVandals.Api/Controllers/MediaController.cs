using Microsoft.AspNetCore.Authorization;
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

    // GET: api/media
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MediaResponseDto>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? tag,
        [FromQuery] bool includeArchived = false)
    {
        var query = _context.MediaItems.AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(m => !m.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(m => m.Category.ToLower() == category.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(m => m.Hashtags.Contains(tag.ToLower()));
        }

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MediaResponseDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Type = m.Type,
                Url = m.Url,
                Category = m.Category,
                Hashtags = m.Hashtags,
                IsArchived = m.IsArchived,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    // GET: api/media/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MediaResponseDto>> GetById(Guid id)
    {
        var item = await _context.MediaItems.FindAsync(id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        return Ok(new MediaResponseDto
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            Type = item.Type,
            Url = item.Url,
            Category = item.Category,
            Hashtags = item.Hashtags,
            IsArchived = item.IsArchived,
            CreatedAt = item.CreatedAt
        });
    }

    // POST: api/media/upload
    [Authorize]
    [HttpPost("upload")]
    public async Task<ActionResult<MediaResponseDto>> Upload([FromForm] CreateMediaDto dto)
    {
        FileUploadResult uploadResult;

        if (dto.Type == MediaType.Photo)
        {
            uploadResult = await _fileStorageService.UploadImageAsync(dto.File);
        }
        else
        {
            uploadResult = await _fileStorageService.UploadVideoAsync(dto.File);
        }

        if (!uploadResult.Success)
            return BadRequest(new { message = uploadResult.Error });

        var mediaItem = new MediaItem
        {
            Title = dto.Title,
            Description = dto.Description,
            Type = dto.Type,
            Category = dto.Category,
            Hashtags = dto.Hashtags ?? new List<string>(),
            Url = uploadResult.Url,
            PublicId = uploadResult.PublicId
        };

        _context.MediaItems.Add(mediaItem);
        await _context.SaveChangesAsync();

        var response = new MediaResponseDto
        {
            Id = mediaItem.Id,
            Title = mediaItem.Title,
            Description = mediaItem.Description,
            Type = mediaItem.Type,
            Url = mediaItem.Url,
            Category = mediaItem.Category,
            Hashtags = mediaItem.Hashtags,
            IsArchived = mediaItem.IsArchived,
            CreatedAt = mediaItem.CreatedAt
        };

        return CreatedAtAction(nameof(GetById), new { id = mediaItem.Id }, response);
    }

    // PATCH: api/media/{id}/archive
    [Authorize]
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

    // DELETE: api/media/{id}
    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.MediaItems.FindAsync(id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        // 1. Delete the physical file from Cloudinary
        await _fileStorageService.DeleteFileAsync(item.PublicId);

        // 2. Delete the record from PostgreSQL
        _context.MediaItems.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // PUT: api/media/{id}
    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MediaResponseDto>> Update(Guid id, [FromBody] UpdateMediaDto dto)
    {
        var item = await _context.MediaItems.FindAsync(id);

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
            Type = item.Type,
            Url = item.Url,
            Category = item.Category,
            Hashtags = item.Hashtags,
            IsArchived = item.IsArchived,
            CreatedAt = item.CreatedAt
        });
    }
}