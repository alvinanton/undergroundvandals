using System.Text;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using UndergroundVandals.Api.Data;

namespace UndergroundVandals.Bot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

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
        var chatId = _configuration.GetValue<long>("TelegramBot:AdminChatId");

        // Run the initial report upon service startup
        try
        {
            await SendDailyReportAsync(chatId, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating or sending initial report.");
        }

        // Daily execution loop (every 24 hours)
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);

            try
            {
                await SendDailyReportAsync(chatId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating or sending daily report.");
            }
        }
    }

    private async Task SendDailyReportAsync(long chatId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var since = DateTime.UtcNow.AddDays(-1);

        // Metric 1: Total Visits
        var totalVisits = await dbContext.WebsiteVisits
            .Where(v => v.CreatedAt >= since)
            .CountAsync(cancellationToken);

        // Metric 2: Unique Visitors (based on UserHash)
        var uniqueVisitors = await dbContext.WebsiteVisits
            .Where(v => v.CreatedAt >= since)
            .Select(v => v.UserHash)
            .Distinct()
            .CountAsync(cancellationToken);

        // Metric 3: Top Countries
        var topCountries = await dbContext.WebsiteVisits
            .Where(v => v.CreatedAt >= since)
            .GroupBy(v => v.CountryCode)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(cancellationToken);

        // Metric 4: Top Visited Pages
        var topPages = await dbContext.WebsiteVisits
            .Where(v => v.CreatedAt >= since)
            .GroupBy(v => v.Path)
            .Select(g => new { Path = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(cancellationToken);

        // Build HTML message
        var sb = new StringBuilder();
        sb.AppendLine("📊 <b>Daily Traffic Report - Underground Vandals</b>");
        sb.AppendLine($"<i>Last 24 hours ({DateTime.UtcNow:yyyy-MM-dd})</i>\n");
        sb.AppendLine($"👁️ <b>Total Visits:</b> {totalVisits}");
        sb.AppendLine($"👤 <b>Unique Visitors:</b> {uniqueVisitors}\n");

        sb.AppendLine("📄 <b>Top Visited Pages:</b>");
        if (topPages.Any())
        {
            foreach (var p in topPages)
            {
                sb.AppendLine($" • <code>{p.Path}</code>: {p.Count} visits");
            }
        }
        else
        {
            sb.AppendLine(" <i>No page data recorded in the last 24 hours.</i>");
        }

        sb.AppendLine("\n🌍 <b>Top Countries:</b>");
        if (topCountries.Any())
        {
            foreach (var c in topCountries)
            {
                sb.AppendLine($" • <code>{c.Country}</code>: {c.Count} visits");
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
            cancellationToken: cancellationToken
        );

        _logger.LogInformation("Daily traffic report sent to Telegram.");
    }
}