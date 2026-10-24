using System.Collections.Concurrent;
using System.Security.Cryptography;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace UndergroundVandals.Bot.Commands;

public sealed record CommandContext(ITelegramBotClient Bot, long ChatId, long UserId, string Args)
{
    public Task ReplyAsync(string html, CancellationToken ct) =>
        Bot.SendMessage(ChatId, html, parseMode: ParseMode.Html, cancellationToken: ct);
}


public interface IBotCommand
{
    string Name { get; }
    string Description { get; }
    string? Usage => null;

    IReadOnlyCollection<string> Aliases => [];

    Task ExecuteAsync(CommandContext ctx, CancellationToken ct);
}

public sealed class CommandDispatcher(IServiceScopeFactory scopeFactory, ILogger<CommandDispatcher> logger)
{
    public async Task DispatchAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        var text = message.Text!.Trim();
        if (!text.StartsWith('/')) return;

        var parts = text.Split(' ', 2, StringSplitOptions.TrimEntries);
        var name = parts[0][1..].Split('@')[0].ToLowerInvariant();
        var args = parts.Length > 1 ? parts[1] : "";

        var ctx = new CommandContext(bot, message.Chat.Id, message.From!.Id, args);

        using var scope = scopeFactory.CreateScope();
        var command = scope.ServiceProvider.GetServices<IBotCommand>()
            .FirstOrDefault(c => c.Name == name || c.Aliases.Contains(name));

        if (command is null)
        {
            await ctx.ReplyAsync("Unknown command. Use /help.", ct);
            return;
        }

        logger.LogInformation("Admin command /{Command} by {UserId}. Args: {Args}", command.Name, ctx.UserId, args);

        try
        {
            await command.ExecuteAsync(ctx, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command /{Command} failed.", command.Name);
            await ctx.ReplyAsync("❌ Command failed. Check the logs.", ct);
        }
    }
}


public sealed class ConfirmationStore
{
    private sealed record Pending(long UserId, DateTime ExpiresAt, Func<IServiceProvider, CancellationToken, Task<string>> Action);

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<string, Pending> _pending = new();

    public string Create(long userId, Func<IServiceProvider, CancellationToken, Task<string>> action)
    {
        var now = DateTime.UtcNow;
        foreach (var (key, item) in _pending)
            if (item.ExpiresAt < now) _pending.TryRemove(key, out _);

        var code = RandomNumberGenerator.GetInt32(1000, 10000).ToString();
        _pending[code] = new Pending(userId, now + Ttl, action);
        return code;
    }

    public Func<IServiceProvider, CancellationToken, Task<string>>? Take(long userId, string code)
    {
        if (_pending.TryRemove(code.Trim(), out var p) && p.UserId == userId && p.ExpiresAt >= DateTime.UtcNow)
            return p.Action;

        return null;
    }
}

public enum ReportKind
{
    Yesterday,
    Today
}

public interface IReportBuilder
{
    Task<string> BuildAsync(ReportKind kind, CancellationToken ct);
}