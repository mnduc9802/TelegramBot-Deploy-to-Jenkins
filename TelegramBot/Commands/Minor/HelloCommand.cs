using Telegram.Bot.Types;
using Telegram.Bot;

namespace TelegramBot.Commands.MinorCommands
{
    public class HelloCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var user = message.From;

            // Lấy thông tin username, first name, last name
            string username = user?.Username;
            string fullName = $"{user?.FirstName} {user?.LastName}".Trim();

            // Tạo tin nhắn trả lời với kiểm tra username
            string replyMessage = !string.IsNullOrEmpty(username)
                ? $"Xin chào {username}, {fullName}! Vui lòng bấm vào Menu để thực hiện các yêu cầu.\n - Telegram Bot by mnduc9802"
                : $"Xin chào, {fullName}! Vui lòng bấm vào Menu để thực hiện các yêu cầu.\n - Telegram Bot by mnduc9802";

            // Gửi tin nhắn trả lời lại người dùng
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: replyMessage,
                cancellationToken: cancellationToken);
        }
    }
}