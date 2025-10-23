import os
import json
from datetime import datetime
from aiogram import Bot, Dispatcher, types
from aiogram.filters import Command
from aiohttp import web

# ====== Переменные окружения ======
TOKEN = os.getenv("TOKEN")
PORT = int(os.getenv("PORT", 8000))

DATA_FILE = "expenses.json"

# ====== Загрузка сохранённых данных ======
if os.path.exists(DATA_FILE):
    with open(DATA_FILE, "r", encoding="utf-8") as f:
        expenses = json.load(f)
else:
    expenses = {}

# ====== Инициализация бота ======
bot = Bot(token=TOKEN)
dp = Dispatcher()

# ====== Вспомогательные функции ======
def get_month_key():
    now = datetime.now()
    return f"{now.year}-{now.month:02d}"

def get_chat_key(chat_id):
    return f"{get_month_key()}_{chat_id}"

def save_data():
    with open(DATA_FILE, "w", encoding="utf-8") as f:
        json.dump(expenses, f, ensure_ascii=False, indent=2)

def add_expense(amount, chat_id):
    key = get_chat_key(chat_id)
    chat_data = expenses.get(key, {"items": [], "total": 0})
    chat_data["items"].append(amount)
    chat_data["total"] = round(chat_data["total"] + amount, 2)
    expenses[key] = chat_data
    save_data()
    return chat_data["total"]

def undo_last(chat_id):
    key = get_chat_key(chat_id)
    chat_data = expenses.get(key)
    if not chat_data or not chat_data["items"]:
        return None
    last = chat_data["items"].pop()
    chat_data["total"] = round(chat_data["total"] - last, 2)
    expenses[key] = chat_data
    save_data()
    return last, chat_data["total"]

# ====== Хэндлеры ======
@dp.message(Command("start"))
async def start(message: types.Message):
    await message.answer(
        "Привет! 💰 Отправь сумму покупки, а я посчитаю, сколько вы потратили в этом месяце.\n"
        "Чтобы отменить последнюю запись, отправь /отмена или /undo"
    )

@dp.message(Command("отмена", "undo"))
async def cancel_last(message: types.Message):
    result = undo_last(message.chat.id)
    if not result:
        await message.reply("Нет записей для отмены 😅")
        return
    last, total = result
    now = datetime.now()
    month_name = now.strftime("%B")
    await message.reply(f"Отменено: {last:.2f} €. Теперь потрачено за {month_name}: {total:.2f} €")

@dp.message()
async def handle_expense(message: types.Message):
    try:
        amount = float(message.text.replace(",", "."))
        total = add_expense(amount, message.chat.id)
        now = datetime.now()
        month_name = now.strftime("%B")
        await message.reply(f"Потрачено за {month_name}: {total:.2f} €")
    except ValueError:
        await message.reply("Пожалуйста, отправь только число, например: 12.5")

# ====== Вебхук сервер ======
async def handle(request: web.Request):
    body = await request.json()
    update = types.Update(**body)
    await dp.feed_update(bot, update)  # ⚡ важно: передаём bot
    return web.Response(text="OK")

app = web.Application()
app.router.add_post("/webhook", handle)

# ====== Запуск приложения ======
if __name__ == "__main__":
    print(f"Starting webhook on port {PORT}")
    web.run_app(app, port=PORT)
