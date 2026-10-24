using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UndergroundVandals.Api.Data;

namespace UndergroundVandals.Bot;

public class Worker : BackgroundService
{
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
                    await SendDailyReportAsync(_adminChatId, stoppingToken);
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
        if (command is not ("/report" or "/stats"))
            return;

        if (!await _reportLock.WaitAsync(0, ct))
        {
            await botClient.SendMessage(_adminChatId, "⏳ A report is already being generated.", cancellationToken: ct);
            return;
        }

        try
        {
            _logger.LogInformation("Manual report request received via Telegram.");
            await botClient.SendMessage(_adminChatId, "⏳ Generating report on demand...", cancellationToken: ct);
            await SendDailyReportAsync(_adminChatId, ct);
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

    private async Task SendDailyReportAsync(long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var since = now.AddDays(-1);

        var visits = db.WebsiteVisits.AsNoTracking().Where(v => v.CreatedAt >= since);

        var totalVisits = await visits.CountAsync(ct);
        var uniqueVisitors = await visits.Select(v => v.UserHash).Distinct().CountAsync(ct);

        var topCountries = await visits
            .GroupBy(v => v.CountryCode)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Country)
            .Take(5)
            .ToListAsync(ct);

        var topPages = await visits
            .GroupBy(v => v.Path)
            .Select(g => new { Path = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Path)
            .Take(5)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("📊 <b>Daily Traffic Report - Underground Vandals</b>");
        sb.AppendLine($"<i>Last 24 hours ({now:yyyy-MM-dd HH:mm} UTC)</i>\n");
        sb.AppendLine($"👁️ <b>Total Visits:</b> {totalVisits}");
        sb.AppendLine($"👤 <b>Unique Visitors:</b> {uniqueVisitors}\n");

        sb.AppendLine("📄 <b>Top Visited Pages:</b>");
        if (topPages.Count > 0)
        {
            foreach (var p in topPages)
                sb.AppendLine($" • <code>{WebUtility.HtmlEncode(p.Path)}</code>: {p.Count} visits");
        }
        else
        {
            sb.AppendLine(" <i>No page data recorded in the last 24 hours.</i>");
        }

        sb.AppendLine("\n🌍 <b>Top Countries:</b>");
        if (topCountries.Count > 0)
        {
            foreach (var c in topCountries)
            {
                var country = c.Country == "XX" ? "Unknown" : c.Country;
                sb.AppendLine($" • <code>{WebUtility.HtmlEncode(country)}</code>: {c.Count} visits");
            }
        }
        else
        {
            sb.AppendLine(" <i>No visits recorded in the last 24 hours.</i>");
        }

        await _botClient.SendMessage(
            chatId: chatId,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        _logger.LogInformation("Daily traffic report sent to Telegram.");
    }
}