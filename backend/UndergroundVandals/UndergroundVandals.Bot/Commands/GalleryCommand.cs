using System.Net;
using System.Text;
using UndergroundVandals.Bot.Services;

namespace UndergroundVandals.Bot.Commands;

public sealed class GalleryCommand(IPostAdminService posts, ILogger<GalleryCommand> logger) : IBotCommand
{
    private const int PageSize = PostPreview.MaxAlbumItems;

    public string Name => "gallery";
    public string Description => "Album with the cover of each post (10 per page)";
    public string? Usage => "[page]";

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var page = int.TryParse(ctx.Args, out var p) && p > 0 ? p : 1;
        var result = await posts.ListCoversAsync(page, PageSize, ct);

        if (result.Items.Count == 0)
        {
            await ctx.ReplyAsync("No posts found.", ct);
            return;
        }

        var covers = new List<(MediaItem Media, string? Caption)>();
        var legend = new StringBuilder();
        legend.AppendLine($"🖼️ <b>Gallery</b> (page {result.Page}/{result.TotalPages}, {result.Total} total)\n");

        foreach (var post in result.Items)
        {
            var status = post.IsArchived ? "📦" : "🟢";
            var title = WebUtility.HtmlEncode(PostCommandHelpers.Shorten(post.Title, 35));
            var shortId = PostCommandHelpers.ShortId(post.Id);

            if (post.Cover is null)
            {
                legend.AppendLine($"– {status} <code>{shortId}</code> {title} (no media)");
                continue;
            }

            // Captions of an album are only visible when opening each photo,
            // so the legend below repeats the numbers and ids
            var number = covers.Count + 1;
            covers.Add((post.Cover, $"{number}. {status} {shortId} · {title}"));
            legend.AppendLine($"{number}. {status} <code>{shortId}</code> {title}");
        }

        if (page < result.TotalPages)
            legend.AppendLine($"\nNext page: <code>/gallery {page + 1}</code>");

        // A failed preview must not hide the list
        try
        {
            await PostPreview.SendAlbumAsync(ctx, covers, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not send the gallery album for page {Page}.", page);
            await ctx.ReplyAsync("⚠️ Previews unavailable. Showing the list only.", ct);
        }

        await ctx.ReplyAsync(legend.ToString(), ct);
    }
}