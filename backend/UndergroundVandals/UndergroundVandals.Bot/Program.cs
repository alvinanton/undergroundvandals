using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.Services;
using UndergroundVandals.Bot;
using UndergroundVandals.Bot.Commands;
using UndergroundVandals.Bot.Reports;
using UndergroundVandals.Bot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var botToken = builder.Configuration["TelegramBot:BotToken"]
    ?? throw new InvalidOperationException("TelegramBot:BotToken is missing.");

builder.Services.AddHttpClient<ITelegramBotClient, TelegramBotClient>(
    client => new TelegramBotClient(botToken, client));

builder.Services.AddScoped<IReportBuilder, ReportBuilder>();
builder.Services.AddSingleton<ConfirmationStore>();
builder.Services.AddSingleton<CommandDispatcher>();

builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));
builder.Services.AddScoped<IFileStorageService, CloudinaryStorageService>();
builder.Services.AddScoped<IPostAdminService, PostAdminService>();

foreach (var type in typeof(IBotCommand).Assembly.GetTypes()
    .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IBotCommand).IsAssignableFrom(t)))
{
    builder.Services.AddScoped(typeof(IBotCommand), type);
}

builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddHostedService<DailyReportService>();

var app = builder.Build();

app.MapGet("/", () => "Underground Vandals Bot Worker is running.");
app.MapGet("/healthz", () => Results.Ok());

app.Run();