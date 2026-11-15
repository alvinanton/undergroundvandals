using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UndergroundVandals.Bot.Services;

namespace UndergroundVandals.Bot.Commands;

/// <summary>
/// Sends visual previews to Telegram: one photo, or an album (up to 10 items).
/// Sends Azure-hosted images and saved Azure poster images for videos.
/// </summary>
internal static class PostPreview
{
    public const int MaxAlbumItems = 10;

    /// <summary>All the media of one post (first 10), with the post title as caption of the first item.</summary>
    public static async Task SendAsync(CommandContext ctx, PostDetail post, CancellationToken ct)
    {
        if (post.Media.Count == 0) return;

        var items = post.Media
            .Where(m => !m.IsVideo || !string.IsNullOrWhiteSpace(m.ThumbnailUrl))
            .Take(MaxAlbumItems)
            .ToList();
        var caption = BuildCaption(post, items.Count);

        if (items.Count == 0)
        {
            await ctx.Bot.SendMessage(ctx.ChatId, caption, parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        var album = items
            .Select((m, i) => (Media: m, Caption: i == 0 ? caption : null))
            .ToList();

        await SendAlbumAsync(ctx, album, ct);
    }

    /// <summary>
    /// Sends 1 to 10 items. Captions are HTML, so they must be encoded by the caller.
    /// A single item goes as a normal photo because sendMediaGroup requires at least 2.
    /// </summary>
    public static async Task SendAlbumAsync(
        CommandContext ctx,
        IReadOnlyList<(MediaItem Media, string? Caption)> items,
        CancellationToken ct)
    {
        if (items.Count == 0) return;

        if (items.Count == 1)
        {
            await ctx.Bot.SendPhoto(
                ctx.ChatId,
                InputFile.FromUri(PreviewImageUrl(items[0].Media)),
                caption: items[0].Caption,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
            return;
        }

        var album = items
            .Select(x => (IAlbumInputMedia)new InputMediaPhoto(InputFile.FromUri(PreviewImageUrl(x.Media)))
            {
                Caption = x.Caption,
                ParseMode = ParseMode.Html
            })
            .ToList();

        await ctx.Bot.SendMediaGroup(ctx.ChatId, album, cancellationToken: ct);
    }

    private static string BuildCaption(PostDetail post, int shown)
    {
        var caption = $"🖼️ <b>{WebUtility.HtmlEncode(PostCommandHelpers.Shorten(post.Title, 120))}</b>";

        var videos = post.Media.Count(m => m.IsVideo);
        if (videos > 0)
        {
            var videosWithoutPoster = post.Media.Count(m => m.IsVideo && string.IsNullOrWhiteSpace(m.ThumbnailUrl));
            caption += $"\n▶️ {videos} video(s), shown as still frames where a poster is available";
            if (videosWithoutPoster > 0)
                caption += $"\n⚠️ {videosWithoutPoster} video(s) omitted because they have no poster image";
        }

        if (post.Media.Count > shown)
            caption += $"\n📎 Showing the first {shown} of {post.Media.Count}";

        return caption;
    }

    /// <summary>
    /// Videos need a separately uploaded poster because Azure Blob Storage does not
    /// generate video-frame transformations. Images use their Azure URL directly.
    /// </summary>
    internal static string PreviewImageUrl(MediaItem media) =>
        media.IsVideo ? media.ThumbnailUrl! : media.Url;
}
