using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddSingleton(new TelegramBotClient(Environment.GetEnvironmentVariable("TOKEN")!));

var app = builder.Build();
app.MapPost("/webhook", async (Update update, TelegramBotClient bot) =>
{
    if (update.Type == UpdateType.Message && update.Message!.Text != null)
    {
        var text = update.Message.Text.Trim();
        var chatId = update.Message.Chat.Id;

        if (text == "/start")
        {
            await bot.SendTextMessageAsync(chatId, "Привет! 💰 Отправь сумму покупки, и я посчитаю, сколько потрачено в этом месяце.");
            return Results.Ok();
        }

        if (double.TryParse(text.Replace(",", "."), out double amount))
        {
            string file = "expenses.json";
            Dictionary<string, double> data = new();
            if (File.Exists(file))
                data = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(file)) ?? new();

            string monthKey = DateTime.Now.ToString("yyyy-MM");
            data[monthKey] = data.GetValueOrDefault(monthKey, 0) + amount;
            File.WriteAllText(file, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

            await bot.SendTextMessageAsync(chatId, $"Потрачено за {DateTime.Now:MMMM}: {data[monthKey]:0.00} €");
        }
        else
        {
            await bot.SendTextMessageAsync(chatId, "Пожалуйста, отправь только число, например 12.5");
        }
    }
    return Results.Ok();
});

app.Run("http://0.0.0.0:10000");
