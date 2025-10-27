using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    static string DataFile = "expenses.json";
    static JsonDocument Expenses;
    static TelegramBotClient Bot;

    static async Task Main()
    {
        var token = Environment.GetEnvironmentVariable("TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Ошибка: не задан TOKEN в переменных среды");
            return;
        }

        Bot = new TelegramBotClient(token);

        // Загружаем данные
        if (File.Exists(DataFile))
        {
            var json = await File.ReadAllTextAsync(DataFile);
            Expenses = JsonDocument.Parse(json);
        }

        var me = await Bot.GetMeAsync();
        Console.WriteLine($"Бот запущен: @{me.Username}");

        Bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync);

        Console.WriteLine("Нажмите любую клавишу для выхода...");
        Console.ReadKey();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, System.Threading.CancellationToken token)
    {
        if (update.Type != UpdateType.Message || update.Message!.Type != MessageType.Text)
            return;

        var chatId = update.Message.Chat.Id;
        var text = update.Message.Text.Trim();

        if (text == "/start")
        {
            await botClient.SendTextMessageAsync(chatId,
                "Привет! 💰 Отправь сумму покупки, а я посчитаю, сколько вы потратили в этом месяце.\n" +
                "Чтобы отменить последнюю запись, отправь /отмена или /undo");
            return;
        }

        if (text == "/отмена" || text == "/undo")
        {
            await botClient.SendTextMessageAsync(chatId, "Функция отмены пока не реализована");
            return;
        }

        if (decimal.TryParse(text.Replace(",", "."), out var amount))
        {
            await botClient.SendTextMessageAsync(chatId, $"Записано: {amount:F2} €");
            return;
        }

        await botClient.SendTextMessageAsync(chatId, "Пожалуйста, отправь только число, например: 12.5");
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, System.Threading.CancellationToken token)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}
