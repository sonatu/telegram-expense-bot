using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    static async Task Main()
    {
        string token = Environment.GetEnvironmentVariable("TOKEN") 
                       ?? throw new Exception("TOKEN not set");

        var bot = new TelegramBotClient(token);
        Console.WriteLine("Bot started.");

        var expenses = LoadExpenses();

        bot.StartReceiving(
            updateHandler: async (client, update, token1) =>
            {
                if (update.Type != UpdateType.Message || update.Message?.Text == null)
                    return;

                var chatId = update.Message.Chat.Id;
                var text = update.Message.Text.Trim();

                if (text == "/start")
                {
                    await bot.SendTextMessageAsync(chatId, 
                        "Привет! 💰 Отправь сумму покупки, а я посчитаю, сколько вы потратили в этом месяце.\n" +
                        "Чтобы отменить последнюю запись, отправь /отмена или /undo");
                    return;
                }

                if (text == "/отмена" || text == "/undo")
                {
                    if (!expenses.TryGetValue(chatId, out var items) || items.Count == 0)
                    {
                        await bot.SendTextMessageAsync(chatId, "Нет записей для отмены 😅");
                        return;
                    }

                    var last = items[items.Count - 1];
                    items.RemoveAt(items.Count - 1);
                    SaveExpenses(expenses);

                    var total = CalculateTotal(items);
                    await bot.SendTextMessageAsync(chatId, $"Отменено: {last:F2} €. Теперь потрачено: {total:F2} €");
                    return;
                }

                if (decimal.TryParse(text.Replace(',', '.'), out var amount))
                {
                    if (!expenses.ContainsKey(chatId))
                        expenses[chatId] = new List<decimal>();

                    expenses[chatId].Add(amount);
                    SaveExpenses(expenses);

                    var total = CalculateTotal(expenses[chatId]);
                    await bot.SendTextMessageAsync(chatId, $"Потрачено: {total:F2} €");
                }
                else
                {
                    await bot.SendTextMessageAsync(chatId, "Пожалуйста, отправь только число, например: 12.5");
                }
            },
            errorHandler: async (client, exception, token1) =>
            {
                Console.WriteLine(exception.Message);
                await Task.CompletedTask;
            }
        );

        // Ждем бесконечно, чтобы бот продолжал работать
        await Task.Delay(-1);
    }

    static Dictionary<long, List<decimal>> LoadExpenses()
    {
        if (!File.Exists("expenses.json"))
            return new Dictionary<long, List<decimal>>();

        var json = File.ReadAllText("expenses.json");
        return JsonSerializer.Deserialize<Dictionary<long, List<decimal>>>(json) 
               ?? new Dictionary<long, List<decimal>>();
    }

    static void SaveExpenses(Dictionary<long, List<decimal>> expenses)
    {
        var json = JsonSerializer.Serialize(expenses, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("expenses.json", json);
    }

    static decimal CalculateTotal(List<decimal> items)
    {
        decimal total = 0;
        foreach (var item in items)
            total += item;
        return total;
    }
}
