using System.Net;

namespace UndergroundVandals.Bot.Commands;

public sealed class HelpCommand(IServiceProvider sp) : IBotCommand
{
    public string Name => "help";
    public string Description => "List available commands";

    public Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var lines = sp.GetServices<IBotCommand>()
            .OrderBy(c => c.Name)
            .Select(c =>
            {
                var usage = c.Usage is null ? "" : $" <code>{WebUtility.HtmlEncode(c.Usage)}</code>";
                return $"/{c.Name}{usage}\n   <i>{WebUtility.HtmlEncode(c.Description)}</i>";
            });

        return ctx.ReplyAsync("🛠️ <b>Commands</b>\n\n" + string.Join("\n", lines), ct);
    }
}

public sealed class ConfirmCommand(ConfirmationStore store, IServiceProvider sp) : IBotCommand
{
    public string Name => "confirm";
    public string Description => "Confirm a pending destructive action";
    public string? Usage => "<code>";

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var action = store.Take(ctx.UserId, ctx.Args);
        if (action is null)
        {
            await ctx.ReplyAsync("No pending action for that code (it may have expired).", ct);
            return;
        }

        await ctx.ReplyAsync(await action(sp, ct), ct);
    }
}

public abstract class ReportCommandBase(IReportBuilder builder, ReportKind kind) : IBotCommand
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual IReadOnlyCollection<string> Aliases => [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (!await Gate.WaitAsync(0, ct))
        {
            await ctx.ReplyAsync("⏳ A report is already being generated.", ct);
            return;
        }

        try
        {
            await ctx.ReplyAsync(await builder.BuildAsync(kind, ct), ct);
        }
        finally
        {
            Gate.Release();
        }
    }
}

public sealed class ReportCommand(IReportBuilder builder) : ReportCommandBase(builder, ReportKind.Yesterday)
{
    public override string Name => "report";
    public override string Description => "Traffic report for yesterday";
    public override IReadOnlyCollection<string> Aliases => ["stats"];
}

public sealed class TodayCommand(IReportBuilder builder) : ReportCommandBase(builder, ReportKind.Today)
{
    public override string Name => "today";
    public override string Description => "Traffic report for today so far";
}