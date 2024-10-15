using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Services;

namespace TelegramBot.Commands.Minor
{
    public class HelloCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var user = message.From;

            // Kiểm tra xem thông tin người dùng có tồn tại không
            if (user == null)
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Xin chào! Vui lòng bấm vào Menu hoặc dấu [/] ở góc phải, để thực hiện các yêu cầu. \n\nNếu bạn không thấy Menu, hãy /help để hiển thị trợ giúp.\n\n - Telegram Bot by mnduc9802",
                    cancellationToken: cancellationToken);
                return;
            }

            // Xử lý nếu có thông tin người dùng
            string userIdentifier = UserService.GetUserIdentifier(message.Chat);
            string fullName = UserService.GetFullName(user);

            string replyMessage = $"Xin chào {userIdentifier}! Vui lòng bấm vào Menu hoặc dấu [/] ở góc phải, để thực hiện các yêu cầu.\nNếu bạn không thấy Menu, hãy /help để hiển thị trợ giúp.\n - Telegram Bot by mnduc9802";

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: replyMessage,
                cancellationToken: cancellationToken);
        }
    }
}