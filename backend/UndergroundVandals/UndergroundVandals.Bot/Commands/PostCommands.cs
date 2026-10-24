using System.Net;
using System.Text;
using UndergroundVandals.Bot.Services;

namespace UndergroundVandals.Bot.Commands;

internal static class PostCommandHelpers
{
    public static string ShortId(Guid id) => id.ToString()[..8];

    public static string Shorten(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";

    public static (string IdText, string Remainder) SplitArgs(string args)
    {
        var parts = args.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return (parts.Length > 0 ? parts[0] : "", parts.Length > 1 ? parts[1] : "");
    }

    public static async Task<Guid?> ResolveOrReplyAsync(
        IPostAdminService posts, CommandContext ctx, string idText, CancellationToken ct)
    {
        var result = await posts.ResolveAsync(idText, ct);

        switch (result.Status)
        {
            case ResolveStatus.Found:
                return result.Id;
            case ResolveStatus.NotFound:
                await ctx.ReplyAsync("Post not found.", ct);
                break;
            case ResolveStatus.Ambiguous:
                await ctx.ReplyAsync("That id matches more than one post. Use more characters.", ct);
                break;
            default:
                await ctx.ReplyAsync("Invalid id. Use at least 4 characters of the post id (see /posts).", ct);
                break;
        }

        return null;
    }
}

public sealed class PostsCommand(IPostAdminService posts) : IBotCommand
{
    public string Name => "posts";
    public string Description => "List posts, newest first";
    public string? Usage => "[page]";

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var page = int.TryParse(ctx.Args, out var p) && p > 0 ? p : 1;
        var result = await posts.ListAsync(page, 10, ct);

        if (result.Items.Count == 0)
        {
            await ctx.ReplyAsync("No posts found.", ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"🖼️ <b>Posts</b> (page {result.Page}/{result.TotalPages}, {result.Total} total)\n");

        foreach (var x in result.Items)
        {
            var status = x.IsArchived ? "📦" : "🟢";
            var title = WebUtility.HtmlEncode(PostCommandHelpers.Shorten(x.Title, 35));
            sb.AppendLine($"{status} <code>{PostCommandHelpers.ShortId(x.Id)}</code> {title} · {x.MediaCount} media");
        }

        await ctx.ReplyAsync(sb.ToString(), ct);
    }
}

public sealed class PostCommand(IPostAdminService posts, ILogger<PostCommand> logger) : IBotCommand
{
    public string Name => "post";
    public string Description => "Show post details";
    public string? Usage => "<id>";

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var (idText, _) = PostCommandHelpers.SplitArgs(ctx.Args);
        var id = await PostCommandHelpers.ResolveOrReplyAsync(posts, ctx, idText, ct);
        if (id is null) return;

        var post = await posts.GetAsync(id.Value, ct);
        if (post is null)
        {
            await ctx.ReplyAsync("Post not found.", ct);
            return;
        }

        try
        {
            await PostPreview.SendAsync(ctx, post, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not send the preview for post {PostId}.", post.Id);
            await ctx.ReplyAsync("⚠️ Preview unavailable. Showing details only.", ct);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"🖼️ <b>{WebUtility.HtmlEncode(post.Title)}</b>");
        sb.AppendLine($"ID: <code>{post.Id}</code>");
        sb.AppendLine($"Status: {(post.IsArchived ? "📦 archived" : "🟢 visible")}");
        sb.AppendLine($"Category: {WebUtility.HtmlEncode(post.Category)}");
        sb.AppendLine($"Media: {post.MediaCount}");
        sb.AppendLine($"Created: {post.CreatedAt:yyyy-MM-dd HH:mm} UTC");

        if (!string.IsNullOrWhiteSpace(post.Description))
            sb.AppendLine($"\n{WebUtility.HtmlEncode(PostCommandHelpers.Shorten(post.Description, 300))}");

        if (post.Hashtags.Count > 0)
            sb.AppendLine($"\n{string.Join(" ", post.Hashtags.Select(WebUtility.HtmlEncode))}");

        await ctx.ReplyAsync(sb.ToString(), ct);
    }
}

public abstract class SetArchivedCommandBase(IPostAdminService posts, bool archived) : IBotCommand
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public string? Usage => "<id>";

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var (idText, _) = PostCommandHelpers.SplitArgs(ctx.Args);
        var id = await PostCommandHelpers.ResolveOrReplyAsync(posts, ctx, idText, ct);
        if (id is null) return;

        var ok = await posts.SetArchivedAsync(id.Value, archived, ct);
        await ctx.ReplyAsync(ok
            ? $"{(archived ? "📦 Archived" : "🟢 Restored")} post <code>{PostCommandHelpers.ShortId(id.Value)}</code>."
            : "Post not found.", ct);
    }
}

public sealed class ArchiveCommand(IPostAdminService posts) : SetArchivedCommandBase(posts, true)
{
    public override string Name => "archive";
    public override string Description => "Hide a post from the site without deleting it";
}

public sealed class UnarchiveCommand(IPostAdminService posts) : SetArchivedCommandBase(posts, false)
{
    public override string Name => "unarchive";
    public override string Description => "Make an archived post visible again";
}

public sealed class EditCommand(IPostAdminService posts) : IBotCommand
{
    public string Name => "edit";
    public string Description => "Change a post title";
    public string? Usage => "<id> <new title>";

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var (idText, title) = PostCommandHelpers.SplitArgs(ctx.Args);

        if (title.Length is 0 or > 200)
        {
            await ctx.ReplyAsync("Usage: <code>/edit &lt;id&gt; &lt;new title&gt;</code> (max 200 chars)", ct);
            return;
        }

        var id = await PostCommandHelpers.ResolveOrReplyAsync(posts, ctx, idText, ct);
        if (id is null) return;

        var ok = await posts.UpdateTitleAsync(id.Value, title, ct);
        await ctx.ReplyAsync(ok ? $"✏️ Post <code>{PostCommandHelpers.ShortId(id.Value)}</code> updated." : "Post not found.", ct);
    }
}