using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Commands
{
    public class StartCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("List All Projects", "projects"),
                InlineKeyboardButton.WithCallbackData("Deploy Project", "deploy")
            },

            new[]
            {
                InlineKeyboardButton.WithCallbackData("Status", "status"),
                InlineKeyboardButton.WithCallbackData("Help", "help")
            }
        });

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Chào mừng! Vui lòng chọn một trong các lệnh sau:",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
    }
}
