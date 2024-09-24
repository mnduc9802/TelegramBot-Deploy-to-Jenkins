using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Commands.MinorCommands
{
    public class StartCommand
    {
        // Đặt Chat ID của group chung ở đây
        private const long GroupChatId = -4201790625; // Thay thế bằng Chat ID của group của bạn

        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var user = message.From;

            // Lấy thông tin username, first name, last name
            string username = user?.Username;
            string fullName = $"{user?.FirstName} {user?.LastName}".Trim();

            // Sử dụng fullName nếu username không tồn tại
            string displayName = !string.IsNullOrEmpty(username) ? $"@{username}" : fullName;

            // Lấy nội dung tin nhắn của người dùng
            string userMessage = message.Text;

            // Nếu đây là tin nhắn từ cuộc trò chuyện riêng với bot, gửi thông báo vào group chung
            if (message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
            {
                string groupMessage = $"{displayName} vừa chat nội dung: {userMessage}";

                await botClient.SendTextMessageAsync(
                    chatId: GroupChatId,
                    text: groupMessage,
                    cancellationToken: cancellationToken);
            }
        }
    }
}
