using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Commands;

namespace TelegramBot.Utilities
{
    public static class DeployConfirmation
    {
        public static async Task DeployConfirmationKeyboard(ITelegramBotClient botClient, CallbackQuery callbackQuery, int projectIndex, CancellationToken cancellationToken)
        {
            var projects = await ProjectsCommand.GetJenkinsProjectsAsync();
            var project = projects[projectIndex];

            var confirmationKeyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("Yes", $"confirm_yes_{projectIndex}"),
                InlineKeyboardButton.WithCallbackData("No", "confirm_no")
            });

            await botClient.EditMessageTextAsync(
                chatId: callbackQuery.Message.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                text: $"Bạn đã chọn {project}. Bạn có muốn xác nhận triển khai không?",
                replyMarkup: confirmationKeyboard,
                cancellationToken: cancellationToken);
        }

        public static async Task HandleConfirmYesCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var projectIndex = int.Parse(callbackQuery.Data.Split('_')[2]);
            var projects = await ProjectsCommand.GetJenkinsProjectsAsync();
            var selectedProject = projects[projectIndex];

            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
            await DeployCommand.ExecuteAsync(botClient, callbackQuery.Message, selectedProject, cancellationToken);
        }

        public static async Task HandleConfirmNoCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(
                chatId: callbackQuery.Message.Chat.Id,
                text: "Yêu cầu /deploy của bạn đã bị hủy.",
                replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Bắt đầu lại", "start_again")),
                cancellationToken: cancellationToken);

            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
        }
    }
}
