using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var botToken = builder.Configuration["TelegramBot:BotToken"]
    ?? throw new InvalidOperationException("TelegramBot:BotToken variable is missing.");

builder.Services.AddHttpClient("TelegramBot")
    .AddTypedClient<ITelegramBotClient>(client => new TelegramBotClient(botToken, client));

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapGet("/", () => "Underground Vandals Bot Worker is running.");

app.Run();