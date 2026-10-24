using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.Entities;
using UndergroundVandals.Api.Services;

namespace UndergroundVandals.Bot.Services;

public sealed record PostSummary(Guid Id, string Title, string Category, bool IsArchived, int MediaCount, DateTime CreatedAt);

public sealed record PostDetail(
    Guid Id,
    string Title,
    string? Description,
    string Category,
    bool IsArchived,
    IReadOnlyList<string> Hashtags,
    int MediaCount,
    IReadOnlyList<MediaItem> Media,
    DateTime CreatedAt);

public sealed record MediaItem(string Url, bool IsVideo);

public sealed record PostCover(Guid Id, string Title, bool IsArchived, MediaItem? Cover);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public enum ResolveStatus
{
    Found,
    NotFound,
    Ambiguous,
    Invalid
}

public sealed record ResolveResult(ResolveStatus Status, Guid Id = default);

public sealed record DeleteResult(bool Found, int FilesDeleted, IReadOnlyList<string> FailedPublicIds);

public interface IPostAdminService
{
    /// <summary>Turns a short id prefix (min. 4 chars) into a full Guid.</summary>
    Task<ResolveResult> ResolveAsync(string idPrefix, CancellationToken ct);

    Task<PagedResult<PostSummary>> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>Same paging as ListAsync, but returning the cover media of each post.</summary>
    Task<PagedResult<PostCover>> ListCoversAsync(int page, int pageSize, CancellationToken ct);

    Task<PostDetail?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> SetArchivedAsync(Guid id, bool archived, CancellationToken ct);
    Task<bool> UpdateTitleAsync(Guid id, string title, CancellationToken ct);

    /// <summary>Permanently deletes the post and its files from storage.</summary>
    Task<DeleteResult> DeleteAsync(Guid id, CancellationToken ct);
}

// For now it lives in the Bot project. When you extract the shared project, move the interface,
// the records and this class there so the API can use the same logic.
public sealed class PostAdminService(
    AppDbContext db,
    IFileStorageService storage,
    ILogger<PostAdminService> logger) : IPostAdminService
{
    public async Task<ResolveResult> ResolveAsync(string idPrefix, CancellationToken ct)
    {
        var prefix = idPrefix.Trim().ToLowerInvariant();
        if (prefix.Length < 4 || !prefix.All(c => Uri.IsHexDigit(c) || c == '-'))
            return new ResolveResult(ResolveStatus.Invalid);

        // Only the Guid column is loaded and matched in memory, which is fine for a gallery
        var ids = await db.Posts.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        var matches = ids
            .Where(id => id.ToString().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        return matches.Count switch
        {
            0 => new ResolveResult(ResolveStatus.NotFound),
            1 => new ResolveResult(ResolveStatus.Found, matches[0]),
            _ => new ResolveResult(ResolveStatus.Ambiguous)
        };
    }

    public async Task<PagedResult<PostSummary>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.Posts.AsNoTracking();
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostSummary(p.Id, p.Title, p.Category, p.IsArchived, p.MediaAssets.Count, p.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<PostSummary>(items, total, page, pageSize);
    }

    public async Task<PagedResult<PostCover>> ListCoversAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.Posts.AsNoTracking();
        var total = await query.CountAsync(ct);

        // MediaAsset has no order column, so the cover is the first one by Id: arbitrary but stable
        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.IsArchived,
                Cover = p.MediaAssets
                    .OrderBy(a => a.Id)
                    .Select(a => new { a.Url, a.Type })
                    .Take(1)
                    .ToList()
            })
            .ToListAsync(ct);

        var items = rows
            .Select(r => new PostCover(
                r.Id,
                r.Title,
                r.IsArchived,
                r.Cover.Count > 0 ? new MediaItem(r.Cover[0].Url, r.Cover[0].Type == MediaType.Video) : null))
            .ToList();

        return new PagedResult<PostCover>(items, total, page, pageSize);
    }

    public async Task<PostDetail?> GetAsync(Guid id, CancellationToken ct)
    {
        var post = await db.Posts
            .AsNoTracking()
            .Include(p => p.MediaAssets)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return post is null
            ? null
            : new PostDetail(post.Id, post.Title, post.Description, post.Category, post.IsArchived,
                post.Hashtags, post.MediaAssets.Count,
                post.MediaAssets.Select(a => new MediaItem(a.Url, a.Type == MediaType.Video)).ToList(),
                post.CreatedAt);
    }

    public async Task<bool> SetArchivedAsync(Guid id, bool archived, CancellationToken ct) =>
        await db.Posts
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsArchived, archived), ct) > 0;

    public async Task<bool> UpdateTitleAsync(Guid id, string title, CancellationToken ct) =>
        await db.Posts
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Title, title), ct) > 0;

    public async Task<DeleteResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var post = await db.Posts
            .Include(p => p.MediaAssets)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (post is null)
            return new DeleteResult(false, 0, []);

        // Copy what we need before the rows disappear
        var files = post.MediaAssets
            .Select(a => (a.PublicId, ResourceType: a.Type == MediaType.Video ? "video" : "image"))
            .ToList();

        // Database first: the site never shows a post whose files are already gone
        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);

        var failed = new List<string>();
        foreach (var (publicId, resourceType) in files)
        {
            try
            {
                if (!await storage.DeleteFileAsync(publicId, resourceType))
                    failed.Add(publicId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete {PublicId} from storage.", publicId);
                failed.Add(publicId);
            }
        }

        if (failed.Count > 0)
        {
            logger.LogWarning(
                "Post {PostId} deleted but {Count} file(s) may remain in storage: {PublicIds}",
                id, failed.Count, string.Join(", ", failed));
        }

        return new DeleteResult(true, files.Count - failed.Count, failed);
    }
}