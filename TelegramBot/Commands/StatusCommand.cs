using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Commands
{
    public class StatusCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var statusText = "Bot hiện đang hoạt động bình thường.";

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: statusText,
                cancellationToken: cancellationToken);
        }
    }
}