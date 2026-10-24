using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UndergroundVandals.Bot.Commands;

namespace UndergroundVandals.Bot.Services;

public sealed class TelegramBotService(
    ITelegramBotClient bot,
    IConfiguration config,
    CommandDispatcher dispatcher,
    IServiceScopeFactory scopeFactory,
    ILogger<TelegramBotService> logger) : BackgroundService
{
    private long _adminId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _adminId = config.GetValue<long>("TelegramBot:AdminChatId");
        if (_adminId == 0)
        {
            logger.LogCritical("TelegramBot:AdminChatId is missing or invalid. Bot will not start.");
            return;
        }

        await RegisterMenuAsync(stoppingToken);

        bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message],
                DropPendingUpdates = true
            },
            cancellationToken: stoppingToken);

        logger.LogInformation("Telegram command listener started.");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } } message)
            return;

        if (message.From?.Id != _adminId || message.Chat.Id != _adminId)
            return;

        await dispatcher.DispatchAsync(client, message, ct);
    }

    private Task HandleErrorAsync(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken ct)
    {
        logger.LogError(exception, "Error in Telegram client ({Source}).", source);
        return Task.CompletedTask;
    }

    
    private async Task RegisterMenuAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var menu = scope.ServiceProvider.GetServices<IBotCommand>()
                .OrderBy(c => c.Name)
                .Select(c => new BotCommand { Command = c.Name, Description = c.Description })
                .ToList();

            await bot.SetMyCommands(menu, new BotCommandScopeChat { ChatId = _adminId }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not register the Telegram command menu.");
        }
    }
}

public sealed class DailyReportService(
    ITelegramBotClient bot,
    IConfiguration config,
    IServiceScopeFactory scopeFactory,
    ILogger<DailyReportService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var chatId = config.GetValue<long>("TelegramBot:AdminChatId");
        if (chatId == 0) return;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextRun();
                logger.LogInformation("Next scheduled report in {Hours}h {Minutes}m.", (int)delay.TotalHours, delay.Minutes);

                await Task.Delay(delay, stoppingToken);

                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var builder = scope.ServiceProvider.GetRequiredService<IReportBuilder>();
                    var html = await builder.BuildAsync(ReportKind.Yesterday, stoppingToken);

                    await bot.SendMessage(chatId, html, parseMode: ParseMode.Html, cancellationToken: stoppingToken);
                    logger.LogInformation("Daily traffic report sent to Telegram.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error generating or sending scheduled daily report.");
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
        var configTime = config.GetValue<string>("TelegramBot:ReportTimeUtc") ?? "08:00";
        if (!TimeSpan.TryParse(configTime, out var targetTime))
            targetTime = new TimeSpan(8, 0, 0);

        var now = DateTime.UtcNow;
        var nextRun = now.Date.Add(targetTime);

        if (now >= nextRun)
            nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }
}