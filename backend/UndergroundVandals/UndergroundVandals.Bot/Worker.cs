using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UndergroundVandals.Api.Data;

namespace UndergroundVandals.Bot;

public class Worker : BackgroundService
{
    private static readonly char[] SparkChars = ['▁', '▂', '▃', '▄', '▅', '▆', '▇', '█'];

    private readonly ILogger<Worker> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _reportLock = new(1, 1);

    private long _adminChatId;

    public Worker(
        ILogger<Worker> logger,
        ITelegramBotClient botClient,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _botClient = botClient;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _adminChatId = _configuration.GetValue<long>("TelegramBot:AdminChatId");
        if (_adminChatId == 0)
        {
            _logger.LogCritical("TelegramBot:AdminChatId is missing or invalid. Worker will not start.");
            return;
        }

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message],
                DropPendingUpdates = true
            },
            cancellationToken: stoppingToken);

        _logger.LogInformation("Telegram command listener started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextRun();
                _logger.LogInformation("Next scheduled report in {Hours}h {Minutes}m.", (int)delay.TotalHours, delay.Minutes);

                await Task.Delay(delay, stoppingToken);

                try
                {
                    await SendReportAsync(_adminChatId, ReportKind.Yesterday, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error generating or sending scheduled daily report.");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private TimeSpan GetDelayUntilNextRun()
    {
        var configTime = _configuration.GetValue<string>("TelegramBot:ReportTimeUtc") ?? "08:00";
        if (!TimeSpan.TryParse(configTime, out var targetTime))
            targetTime = new TimeSpan(8, 0, 0);

        var now = DateTime.UtcNow;
        var nextRun = now.Date.Add(targetTime);

        if (now >= nextRun)
            nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } message)
            return;

        if (message.From?.Id != _adminChatId || message.Chat.Id != _adminChatId)
            return;

        var command = text.Split(' ', '@')[0].ToLowerInvariant();

        ReportKind kind;
        switch (command)
        {
            case "/report":
            case "/stats":
                kind = ReportKind.Yesterday;
                break;
            case "/today":
                kind = ReportKind.Today;
                break;
            default:
                return;
        }

        if (!await _reportLock.WaitAsync(0, ct))
        {
            await botClient.SendMessage(_adminChatId, "⏳ A report is already being generated.", cancellationToken: ct);
            return;
        }

        try
        {
            _logger.LogInformation("Manual {Kind} report requested via Telegram.", kind);
            await botClient.SendMessage(_adminChatId, "⏳ Generating report on demand...", cancellationToken: ct);
            await SendReportAsync(_adminChatId, kind, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error generating manual report.");
            await botClient.SendMessage(_adminChatId, "❌ Failed to generate the report. Check the logs.", cancellationToken: ct);
        }
        finally
        {
            _reportLock.Release();
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken ct)
    {
        _logger.LogError(exception, "Error in Telegram client ({Source}).", source);
        return Task.CompletedTask;
    }

    private async Task SendReportAsync(long chatId, ReportKind kind, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var todayStart = now.Date;

        DateTime from, to, prevFrom, prevTo;
        string title, subtitle;

        if (kind == ReportKind.Today)
        {
            from = todayStart;
            to = now;
            prevFrom = todayStart.AddDays(-1);
            prevTo = now.AddDays(-1);
            title = "Today so far";
            subtitle = $"{from:yyyy-MM-dd} 00:00 → {now:HH:mm} UTC";
        }
        else
        {
            from = todayStart.AddDays(-1);
            to = todayStart;
            prevFrom = todayStart.AddDays(-2);
            prevTo = from;
            title = "Daily Traffic Report";
            subtitle = $"{from:yyyy-MM-dd} (UTC)";
        }

        var all = db.WebsiteVisits.AsNoTracking();
        var current = all.Where(v => v.CreatedAt >= from && v.CreatedAt < to);
        var previous = all.Where(v => v.CreatedAt >= prevFrom && v.CreatedAt < prevTo);

        var visits = await current.CountAsync(ct);
        var unique = await current.Select(v => v.UserHash).Distinct().CountAsync(ct);
        var prevVisits = await previous.CountAsync(ct);
        var prevUnique = await previous.Select(v => v.UserHash).Distinct().CountAsync(ct);
        var allTime = await all.CountAsync(ct);

        var weekStart = from.Date.AddDays(-6);
        var weekEnd = kind == ReportKind.Today ? now : to;
        var week = all.Where(v => v.CreatedAt >= weekStart && v.CreatedAt < weekEnd);

        var weekVisits = await week.CountAsync(ct);
        var weekUnique = await week.Select(v => v.UserHash).Distinct().CountAsync(ct);

        var perDay = await week
            .GroupBy(v => v.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var dailyCounts = Enumerable.Range(0, 7)
            .Select(i => weekStart.AddDays(i).Date)
            .Select(d => perDay.FirstOrDefault(x => x.Day.Date == d)?.Count ?? 0)
            .ToArray();

        var topCountries = await current
            .GroupBy(v => v.CountryCode)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Country)
            .Take(5)
            .ToListAsync(ct);

        var topPages = await current
            .GroupBy(v => v.Path)
            .Select(g => new { Path = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Path)
            .Take(5)
            .ToListAsync(ct);

        var pagesPerVisitor = unique > 0 ? (double)visits / unique : 0;

        var sb = new StringBuilder();
        sb.AppendLine($"📊 <b>{title} - Underground Vandals</b>");
        sb.AppendLine($"<i>{subtitle}</i>\n");

        sb.AppendLine($"👁️ <b>Visits:</b> {visits} {Trend(visits, prevVisits)}");
        sb.AppendLine($"👤 <b>Unique visitors:</b> {unique} {Trend(unique, prevUnique)}");
        sb.AppendLine($"📑 <b>Pages/visitor:</b> {pagesPerVisitor:0.0}\n");

        sb.AppendLine("📅 <b>Last 7 days</b>");
        var labels = Enumerable.Range(0, 7)
            .Select(i => weekStart.AddDays(i).ToString("ddd", CultureInfo.InvariantCulture)[0]);
        sb.AppendLine($"<code>{Sparkline(dailyCounts)}</code>");
        sb.AppendLine($"<code>{string.Concat(labels)}</code>  ({string.Join(" ", dailyCounts)})");
        sb.AppendLine($" • Visits: {weekVisits}");
        sb.AppendLine($" • Unique: {weekUnique}");
        sb.AppendLine($" • Daily avg: {weekVisits / 7.0:0.#}");
        sb.AppendLine($" • All time: {allTime}\n");

        sb.AppendLine("📄 <b>Top pages:</b>");
        if (topPages.Count > 0)
        {
            foreach (var p in topPages)
                sb.AppendLine($" • <code>{WebUtility.HtmlEncode(p.Path)}</code>: {p.Count}");
        }
        else
        {
            sb.AppendLine(" <i>No data.</i>");
        }

        sb.AppendLine("\n🌍 <b>Top countries:</b>");
        if (topCountries.Count > 0)
        {
            foreach (var c in topCountries)
            {
                var country = c.Country == "XX" ? "Unknown" : c.Country;
                sb.AppendLine($" • <code>{WebUtility.HtmlEncode(country)}</code>: {c.Count}");
            }
        }
        else
        {
            sb.AppendLine(" <i>No data.</i>");
        }

        await _botClient.SendMessage(
            chatId: chatId,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        _logger.LogInformation("{Kind} traffic report sent to Telegram.", kind);
    }

    private static string Trend(int current, int previous)
    {
        if (previous == 0) return current == 0 ? "" : "🆕";
        var pct = (current - previous) * 100.0 / previous;
        var icon = pct > 0 ? "📈" : pct < 0 ? "📉" : "➖";
        return $"{icon} {pct:+0;-0;0}%";
    }

    private static string Sparkline(int[] values)
    {
        var max = values.Max();
        if (max == 0) return new string(SparkChars[0], values.Length);

        return new string(values
            .Select(v => SparkChars[(int)Math.Round(v * (SparkChars.Length - 1) / (double)max)])
            .ToArray());
    }

    private enum ReportKind
    {
        Yesterday,
        Today
    }
}