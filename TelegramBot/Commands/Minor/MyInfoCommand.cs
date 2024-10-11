using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Services;

namespace TelegramBot.Commands.Minor
{
    public class MyInfoCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var user = message.From;
            if (user == null)
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Không thể lấy thông tin người dùng.",
                    cancellationToken: cancellationToken);
                return;
            }

            string userName = UserService.GetUserIdentifier(message.Chat) ?? "Không có";
            string firstName = user.FirstName ?? "Không có";
            string lastName = user.LastName ?? "Không có";

            string infoMessage = $"<b>Thông tin của bạn:</b>\n\n" +
                                $"<b>Username:</b> {userName}\n" +
                                $"<b>UserId:</b> <code>{user.Id}</code>\n" +
                                $"<b>First Name:</b> {firstName}\n" +
                                $"<b>Last Name:</b> {lastName}";

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: infoMessage,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                cancellationToken: cancellationToken);
        }
    }
}