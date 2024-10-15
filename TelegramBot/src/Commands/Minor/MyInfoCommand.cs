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

            string userName = string.IsNullOrEmpty(user.Username) ? "Không có" : $"@{user.Username}";
            string firstName = user.FirstName;
            string lastName = user.LastName ?? "Không có";
            string userRole = await CredentialService.GetUserRoleAsync(user.Id);

            string infoMessage = $"<b>Thông tin của bạn:</b>\n\n" +
                                $"<b>Username:</b> {userName}\n" +
                                $"<b>UserId:</b> <code>{user.Id}</code>\n" +
                                $"<b>First Name:</b> {firstName}\n" +
                                $"<b>Last Name:</b> {lastName}\n" +
                                $"<b>Quyền:</b> {userRole}";

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: infoMessage,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                cancellationToken: cancellationToken);
        }
    }
}