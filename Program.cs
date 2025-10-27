using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;

class Program
{
    static string TOKEN = Environment.GetEnvironmentVariable("TOKEN") ?? throw new Exception("TOKEN not set");
    static string DATA_FILE = "expenses.json";
    static TelegramBotClient Bot = new TelegramBotClient(TOKEN);
    static Dictionary<string, ChatData> Expenses = new Dictionary<string, ChatData>();

    static async Task Main()
    {
        LoadData();

        Bot.StartReceiving(UpdateHandler, ErrorHandler);

        Console.WriteLine("Bot started. Press any key to exit.");
        Console.ReadKey();

        SaveData();
    }

    static async Task UpdateHandler(ITelegramBotClient botClient, Update update, System.Threading.CancellationToken token)
    {
        if (update.Type != UpdateType.Message) return;
        var message = update.Message;
        if (message.Type != MessageType.Text) return;

        if (message.Text.StartsWith("/start"))
        {
            await botClient.SendTextMessageAsync(message.Chat.Id,
                "Привет! 💰 Отправь сумму покупки, а я посчитаю, сколько вы потратили в этом месяце.\n" +
                "Чтобы отменить последнюю запись, отправь /undo");
            return;
        }

        if (message.Text.StartsWith("/undo"))
        {
            var result = UndoLast(message.Chat.Id);
            if (result == null)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Нет записей для отмены 😅");
            }
            else
            {
                var (last, total) = result.Value;
                var monthName = DateTime.Now.ToString("MMMM");
                await botClient.SendTextMessageAsync(message.Chat.Id,
                    $"Отменено: {last:F2} €. Теперь потрачено за {monthName}: {total:F2} €");
            }
            return;
        }

        if (double.TryParse(message.Text.Replace(",", "."), out double amount))
        {
            double total = AddExpense(amount, message.Chat.Id);
            var monthName = DateTime.Now.ToString("MMMM");
            await botClient.SendTextMessageAsync(message.Chat.Id,
                $"Потрачено за {monthName}: {total:F2} €");
        }
        else
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправь только число, например: 12.5");
        }
    }

    static Task ErrorHandler(ITelegramBotClient botClient, Exception exception, System.Threading.CancellationToken token)
    {
        Console.WriteLine(exception);
        return Task.CompletedTask;
    }

    static void LoadData()
    {
        if (System.IO.File.Exists(DATA_FILE))
        {
            var json = System.IO.File.ReadAllText(DATA_FILE);
            Expenses = JsonSerializer.Deserialize<Dictionary<string, ChatData>>(json) ?? new Dictionary<string, ChatData>();
        }
    }

    static void SaveData()
    {
        var json = JsonSerializer.Serialize(Expenses, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(DATA_FILE, json);
    }

    static string GetMonthKey() => DateTime.Now.ToString("yyyy-MM");

    static string GetChatKey(long chatId) => $"{GetMonthKey()}_{chatId}";

    static double AddExpense(double amount, long chatId)
    {
        var key = GetChatKey(chatId);
        if (!Expenses.ContainsKey(key))
            Expenses[key] = new ChatData();

        Expenses[key].Items.Add(amount);
        Expenses[key].Total += amount;
        Expenses[key].Total = Math.Round(Expenses[key].Total, 2);

        SaveData();
        return Expenses[key].Total;
    }

    static (double last, double total)? UndoLast(long chatId)
    {
        var key = GetChatKey(chatId);
        if (!Expenses.ContainsKey(key) || Expenses[key].Items.Count == 0) return null;

        var last = Expenses[key].Items[^1];
        Expenses[key].Items.RemoveAt(Expenses[key].Items.Count - 1);
        Expenses[key].Total = Math.Round(Expenses[key].Total - last, 2);

        SaveData();
        return (last, Expenses[key].Total);
    }

    class ChatData
    {
        public List<double> Items { get; set; } = new List<double>();
        public double Total { get; set; } = 0;
    }
}
