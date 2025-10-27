using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;

var builder = WebApplication.CreateBuilder(args);

// Получаем токен из переменной окружения
var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("❌ BOT_TOKEN not found in environment variables!");
    return;
}

var bot = new TelegramBotClient(token);
var app = builder.Build();

app.MapPost("/webhook", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync();

    var update = Newtonsoft.Json.JsonConvert.DeserializeObject<Update>(body);
    if (update == null)
        return Results.Ok();

    try
    {
        if (update.Type == UpdateType.Message && update.Message?.Text != null)
        {
            var message = update.Message;

            if (message.Text == "/start")
            {
                await bot.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Привет! Я бот учёта расходов 💰"
                );
            }
            else
            {
                await bot.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Ты сказал: {message.Text}"
                );
            }
        }
    }
    catch (ApiRequestException ex)
    {
        Console.WriteLine($"Telegram API Error: {ex.Message}");
    }

    return Results.Ok();
});

app.MapGet("/", () => "✅ Telegram bot is running!");
app.Run("http://0.0.0.0:" + (Environment.GetEnvironmentVariable("PORT") ?? "10000"));
