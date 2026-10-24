using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Bot;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var token = builder.Configuration["TelegramBot:BotToken"]
                ?? throw new InvalidOperationException("Missing TelegramBot:BotToken in appsettings.json");
    return new TelegramBotClient(token);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
