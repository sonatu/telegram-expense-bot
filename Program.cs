using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Получаем токен из переменной окружения
var botToken = Environment.GetEnvironmentVariable("TOKEN");
if (string.IsNullOrEmpty(botToken))
{
    Console.WriteLine("❌ TOKEN not found in environment variables!");
    return;
}

var bot = new TelegramBotClient(botToken);

// Файл для хранения данных
const string DataFile = "expenses.json";

// Структура для хранения трат
var expenses = new Dictionary<string, ExpenseData>();

if (System.IO.File.Exists(DataFile))
{
    try
    {
        var json = System.IO.File.ReadAllText(DataFile);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, ExpenseData>>(json);
        if (loaded != null)
            expenses = loaded;
    }
    catch
    {
        Console.WriteLine("⚠️ Ошибка чтения файла данных, начинается с пустого словаря.");
    }
}

void SaveData()
{
    var json = JsonSerializer.Serialize(expenses, new JsonSerializerOptions { WriteIndented = true });
    System.IO.File.WriteAllText(DataFile, json);
}

string GetMonthKey() => $"{DateTime.Now:yyyy-MM}";

string GetChatKey(long chatId) => $"{GetMonthKey()}_{chatId}";

decimal AddExpense(decimal amount, long chatId)
{
    var key = GetChatKey(chatId);
    if (!expenses.ContainsKey(key))
        expenses[key] = new ExpenseData();

    expenses[key].Items.Add(amount);
    expenses[key].Total = Math.Round(expenses[key].Items.Sum(), 2);
    SaveData();
    return expenses[key].Total;
}

(bool success, decimal last, decimal total) UndoLast(long chatId)
{
    var key = GetChatKey(chatId);
    if (!expenses.ContainsKey(key) || expenses[key].Items.Count == 0)
        return (false, 0, 0);

    var last = expenses[key].Items.Last();
    expenses[key].Items.RemoveAt(expenses[key].Items.Count - 1);
    expenses[key].Total = Math.Round(expenses[key].Items.Sum(), 2);
    SaveData();
    return (true, last, expenses[key].Total);
}

// Главная логика обработки сообщений
app.MapPost("/webhook", async (Update update) =>
{
    if (update.Type != UpdateType.Message || update.Message == null)
        return Results.Ok();

    var message = update.Message;
    var chatId = message.Chat.Id;
    var text = message.Text?.Trim();

    if (string.IsNullOrEmpty(text))
        return Results.Ok();

    if (text.StartsWith("/start"))
    {
        await bot.SendMessage(chatId,
            "Привет! 💰 Отправь сумму покупки, и я посчитаю, сколько вы потратили в этом месяце.\n" +
            "Чтобы отменить последнюю запись, отправь /undo");
    }
    else if (text.StartsWith("/undo") || text.StartsWith("/отмена"))
    {
        var result = UndoLast(chatId);
        if (!result.success)
        {
            await bot.SendMessage(chatId, "Нет записей для отмены 😅");
        }
        else
        {
            var monthName = DateTime.Now.ToString("MMMM");
            await bot.SendMessage(chatId, $"Отменено: {result.last:F2} €. Теперь потрачено за {monthName}: {result.total:F2} €");
        }
    }
    else
    {
        if (decimal.TryParse(text.Replace(",", "."), out var amount))
        {
            var total = AddExpense(amount, chatId);
            var monthName = DateTime.Now.ToString("MMMM");
            await bot.SendMessage(chatId, $"Потрачено за {monthName}: {total:F2} €");
        }
        else
        {
            await bot.SendMessage(chatId, "Пожалуйста, отправь только число, например: 12.5");
        }
    }

    return Results.Ok();
});

// Проверочный маршрут (можно открыть в браузере)
app.MapGet("/", () => "✅ Telegram Expense Bot is running!");

// Запуск
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
Console.WriteLine($"🚀 Starting server on port {port}");
app.Run($"http://0.0.0.0:{port}");

// Модель для данных
public class ExpenseData
{
    public List<decimal> Items { get; set; } = new();
    public decimal Total { get; set; }
}
