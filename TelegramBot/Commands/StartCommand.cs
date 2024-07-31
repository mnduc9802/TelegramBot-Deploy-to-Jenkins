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
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Chào mừng! Vui lòng bấm vào Menu để thực hiện các yêu cầu.\n - Telegram Bot by mnduc9802",
                cancellationToken: cancellationToken);
        }
    }
}
