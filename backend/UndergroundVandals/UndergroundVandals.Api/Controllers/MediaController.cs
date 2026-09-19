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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PostResponseDto>>> GetAll(
    [FromQuery] string? category,
    [FromQuery] string? tag,
    [FromQuery] bool includeArchived = false)
    {
        var query = _context.Posts
            .Include(p => p.MediaAssets)
            .AsQueryable();

        if (!includeArchived)
            query = query.Where(m => !m.IsArchived);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(m => m.Category.ToLower() == category.ToLower());

        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(m => m.Hashtags.Contains(tag.ToLower()));

        var posts = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var items = posts.Select(MapToDto).ToList();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostResponseDto>> GetById(Guid id)
    {
        var item = await _context.Posts
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        return Ok(MapToDto(item));
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpGet("upload-signature")]
    public IActionResult GetUploadSignature([FromQuery] string folder = "underground_vandals/photos")
    {
        var uploadParams = _fileStorageService.GenerateUploadParameters(folder);
        return Ok(uploadParams);
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPost("upload")]
    public async Task<ActionResult<PostResponseDto>> Upload([FromBody] CreatePostDto dto)
    {
        if (dto.Assets == null || !dto.Assets.Any())
            return BadRequest(new { message = "At least one asset is required." });

        var post = new Post
        {
            Title = dto.Title,
            Description = dto.Description,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category,
            Hashtags = dto.Hashtags ?? new List<string>()
        };

        foreach (var assetDto in dto.Assets)
        {
            var isVideo = assetDto.Type.Equals("video", StringComparison.OrdinalIgnoreCase);

            post.MediaAssets.Add(new MediaAsset
            {
                Url = assetDto.Url,
                PublicId = assetDto.PublicId,
                Type = isVideo ? MediaType.Video : MediaType.Photo
            });
        }

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = post.Id }, MapToDto(post));
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPost("{id:guid}/assets")]
    public async Task<ActionResult<PostResponseDto>> AddAssets(Guid id, [FromBody] AddAssetsDto dto)
    {
        if (dto.Assets == null || !dto.Assets.Any())
            return BadRequest(new { message = "At least one asset is required." });

        var item = await _context.Posts
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        foreach (var assetDto in dto.Assets)
        {
            var isVideo = assetDto.Type.Equals("video", StringComparison.OrdinalIgnoreCase);

            item.MediaAssets.Add(new MediaAsset
            {
                Url = assetDto.Url,
                PublicId = assetDto.PublicId,
                Type = isVideo ? MediaType.Video : MediaType.Photo
            });
        }

        await _context.SaveChangesAsync();

        return Ok(MapToDto(item));
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
            var resourceType = asset.Type == MediaType.Video ? "video" : "image";
            await _fileStorageService.DeleteFileAsync(asset.PublicId, resourceType);
        }

        _context.MediaAssets.Remove(asset);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPatch("{id:guid}/archive")]
    public async Task<IActionResult> ToggleArchive(Guid id)
    {
        var item = await _context.Posts.FindAsync(id);

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
        var item = await _context.Posts
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        foreach (var asset in item.MediaAssets)
        {
            if (!string.IsNullOrWhiteSpace(asset.PublicId))
            {
                var resourceType = asset.Type == MediaType.Video ? "video" : "image";
                await _fileStorageService.DeleteFileAsync(asset.PublicId, resourceType);
            }
        }

        _context.Posts.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PostResponseDto>> Update(Guid id, [FromBody] UpdatePostDto dto)
    {
        var item = await _context.Posts
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new { message = "Media item not found." });

        item.Title = dto.Title;
        item.Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category;
        item.Description = dto.Description;
        item.Hashtags = dto.Hashtags ?? new List<string>();

        await _context.SaveChangesAsync();

        return Ok(MapToDto(item));
    }

    private static PostResponseDto MapToDto(Post post)
    {
        var assets = post.MediaAssets.Select(a => new MediaAssetDto
        {
            Id = a.Id,
            Url = a.Url,
            Type = a.Type == MediaType.Photo ? "image" : "video"
        }).ToList();

        return new PostResponseDto
        {
            Id = post.Id,
            Title = post.Title,
            Description = post.Description,
            Category = post.Category,
            Hashtags = post.Hashtags,
            IsArchived = post.IsArchived,
            CreatedAt = post.CreatedAt,
            Media = assets,
            MediaAssets = assets
        };
    }
}