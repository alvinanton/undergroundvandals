using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.DTOs;
using UndergroundVandals.Api.Entities;
using UndergroundVandals.Api.Services;

namespace UndergroundVandals.Api.Controllers;

[Tags("Media")]
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
    [EndpointSummary("List posts")]
    [EndpointDescription("Public. Optional category, tag, and includeArchived filters (false by default), combined with AND. No pagination.")]
    [ProducesResponseType(typeof(IEnumerable<PostResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
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
    [EndpointSummary("Get post")]
    [EndpointDescription("Public. Looks up a post by GUID, including archived posts.")]
    [ProducesResponseType(typeof(PostResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostResponseDto>> GetById(Guid id)
    {
        var item = await _context.Posts
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new ApiMessageResponseDto("Media item not found."));

        return Ok(MapToDto(item));
    }

    /// <summary>
    /// Generates upload parameters (SAS URL) for Azure Blob Storage.
    /// Expects explicit target path, e.g., 'archive-media/photos' or 'gallery-media/articles'.
    /// </summary>
    [Authorize(Roles = "Admin,Editor")]
    [HttpGet("upload-signature")]
    [EndpointSummary("Generate upload authorization")]
    [EndpointDescription("Admin or Editor. Requires target in container/folder format. Returns uploadUrl, fileUrl, publicId, and provider for a direct upload to Azure; the SAS is valid for 15 minutes.")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest, Description = "Automatic validation error (ValidationProblemDetails) or a controller error with a message field.")]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUploadSignature([FromQuery] string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return BadRequest(new ApiMessageResponseDto("Target path (e.g., 'archive-media/photos') is required."));

        var uploadParams = await _fileStorageService.GenerateUploadParametersAsync(target);
        return Ok(uploadParams);
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPost("upload")]
    [EndpointSummary("Create post with assets")]
    [EndpointDescription("Admin or Editor. Accepts JSON metadata rather than file bytes. Upload files to Azure first. Requires a title and at least one asset.")]
    [ProducesResponseType(typeof(PostResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest, Description = "Automatic validation error (ValidationProblemDetails) or a controller error with a message field.")]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PostResponseDto>> Upload([FromBody] CreatePostDto dto)
    {
        if (dto.Assets == null || !dto.Assets.Any())
            return BadRequest(new ApiMessageResponseDto("At least one asset is required."));

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
                PublicId = assetDto.PublicId, // Target relative path: e.g. "archive-media/photos/guid"
                Type = isVideo ? MediaType.Video : MediaType.Photo,
                IsCover = assetDto.IsCover,
                ThumbnailUrl = assetDto.ThumbnailUrl
            });
        }

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = post.Id }, MapToDto(post));
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPost("{id:guid}/assets")]
    [EndpointSummary("Add assets to a post")]
    [EndpointDescription("Admin or Editor. Adds at least one asset previously uploaded to Azure, preserving existing assets.")]
    [ProducesResponseType(typeof(PostResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest, Description = "Automatic validation error (ValidationProblemDetails) or a controller error with a message field.")]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostResponseDto>> AddAssets(Guid id, [FromBody] AddAssetsDto dto)
    {
        if (dto.Assets == null || !dto.Assets.Any())
            return BadRequest(new ApiMessageResponseDto("At least one asset is required."));

        var item = await _context.Posts
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new ApiMessageResponseDto("Media item not found."));

        foreach (var assetDto in dto.Assets)
        {
            var isVideo = assetDto.Type.Equals("video", StringComparison.OrdinalIgnoreCase);

            item.MediaAssets.Add(new MediaAsset
            {
                Url = assetDto.Url,
                PublicId = assetDto.PublicId,
                Type = isVideo ? MediaType.Video : MediaType.Photo,
                IsCover = assetDto.IsCover,
                ThumbnailUrl = assetDto.ThumbnailUrl
            });
        }

        await _context.SaveChangesAsync();

        return Ok(MapToDto(item));
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpDelete("assets/{assetId:guid}")]
    [EndpointSummary("Delete asset")]
    [EndpointDescription("Admin or Editor. Deletes the primary file, its thumbnail, and the asset record. May leave the post without assets.")]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsset(Guid assetId)
    {
        var asset = await _context.MediaAssets.FindAsync(assetId);

        if (asset == null)
            return NotFound(new ApiMessageResponseDto("Asset not found."));

        // 1. Delete primary cloud file
        if (!string.IsNullOrWhiteSpace(asset.PublicId))
        {
            var resourceType = asset.Type == MediaType.Video ? "video" : "image";
            await _fileStorageService.DeleteFileAsync(asset.PublicId, resourceType);
        }

        // 2. Delete custom thumbnail/poster if present
        if (!string.IsNullOrWhiteSpace(asset.ThumbnailUrl))
        {
            var thumbPublicId = ExtractPublicIdFromUrl(asset.ThumbnailUrl);
            await _fileStorageService.DeleteFileAsync(thumbPublicId, "image");
        }

        _context.MediaAssets.Remove(asset);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPatch("{id:guid}/archive")]
    [EndpointSummary("Toggle archived status")]
    [EndpointDescription("Admin or Editor. No request body. Each call toggles isArchived; repeating the request reverses the result.")]
    [ProducesResponseType(typeof(ArchiveStatusResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleArchive(Guid id)
    {
        var item = await _context.Posts.FindAsync(id);

        if (item == null)
            return NotFound(new ApiMessageResponseDto("Media item not found."));

        item.IsArchived = !item.IsArchived;
        await _context.SaveChangesAsync();

        return Ok(new ArchiveStatusResponseDto(
            item.IsArchived ? "Media item successfully archived." : "Media item successfully unarchived.",
            item.Id,
            item.IsArchived));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete post")]
    [EndpointDescription("Admin only. Deletes the post, its assets, and associated files. Storage and database operations do not share a transaction.")]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.Posts
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new ApiMessageResponseDto("Media item not found."));

        foreach (var asset in item.MediaAssets)
        {
            // 1. Delete primary file from Azure Blob Storage
            if (!string.IsNullOrWhiteSpace(asset.PublicId))
            {
                var resourceType = asset.Type == MediaType.Video ? "video" : "image";
                await _fileStorageService.DeleteFileAsync(asset.PublicId, resourceType);
            }

            // 2. Delete poster frame/thumbnail if present
            if (!string.IsNullOrWhiteSpace(asset.ThumbnailUrl))
            {
                var thumbPublicId = ExtractPublicIdFromUrl(asset.ThumbnailUrl);
                await _fileStorageService.DeleteFileAsync(thumbPublicId, "image");
            }
        }

        _context.Posts.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "Admin,Editor")]
    [HttpPut("{id:guid}")]
    [EndpointSummary("Update post")]
    [EndpointDescription("Admin or Editor. Replaces the title, description, category, and hashtags. Does not modify assets or archived status.")]
    [ProducesResponseType(typeof(PostResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostResponseDto>> Update(Guid id, [FromBody] UpdatePostDto dto)
    {
        var item = await _context.Posts
            .Include(m => m.MediaAssets)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item == null)
            return NotFound(new ApiMessageResponseDto("Media item not found."));

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
            Type = a.Type == MediaType.Photo ? "image" : "video",
            IsCover = a.IsCover,
            ThumbnailUrl = a.ThumbnailUrl
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

    private static string ExtractPublicIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.AbsolutePath.TrimStart('/');
        }
        return url;
    }
}
