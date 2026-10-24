using System.Net;
using UndergroundVandals.Bot.Services;

namespace UndergroundVandals.Bot.Commands;

public sealed class DeleteCommand(IPostAdminService posts, ConfirmationStore confirmations) : IBotCommand
{
    public string Name => "delete";
    public string Description => "Permanently delete a post and its files (asks to confirm)";
    public string? Usage => "<id>";

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var (idText, _) = PostCommandHelpers.SplitArgs(ctx.Args);
        var resolved = await PostCommandHelpers.ResolveOrReplyAsync(posts, ctx, idText, ct);
        if (resolved is null) return;

        var postId = resolved.Value;
        var post = await posts.GetAsync(postId, ct);
        if (post is null)
        {
            await ctx.ReplyAsync("Post not found.", ct);
            return;
        }

        var code = confirmations.Create(ctx.UserId, async (sp, token) =>
        {
            var result = await sp.GetRequiredService<IPostAdminService>().DeleteAsync(postId, token);
            if (!result.Found)
                return "Post not found.";

            var message = $"🗑️ Post <code>{PostCommandHelpers.ShortId(postId)}</code> deleted. " +
                          $"{result.FilesDeleted} file(s) removed from storage.";

            if (result.FailedPublicIds.Count > 0)
                message += $"\n⚠️ {result.FailedPublicIds.Count} file(s) could not be removed. Their ids are in the logs.";

            return message;
        });

        await ctx.ReplyAsync(
            $"⚠️ <b>Permanent action</b>\n" +
            $"Delete <code>{PostCommandHelpers.ShortId(postId)}</code> <i>{WebUtility.HtmlEncode(post.Title)}</i> " +
            $"and its {post.MediaCount} media file(s)? This cannot be undone.\n\n" +
            $"Send <code>/confirm {code}</code> within 60 seconds. Use /archive if you only want to hide it.", ct);
    }
}