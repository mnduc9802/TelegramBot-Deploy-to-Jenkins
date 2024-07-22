using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

public class StartCommand
{
    public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Deploy Project", "deploy"),
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