//using Telegram.Bot;
//using Telegram.Bot.Types;
//using Telegram.Bot.Types.ReplyMarkups;
//using TelegramBot.Commands;

//namespace TelegramBot.Utilities
//{
//    public static class DeploymentConfirmation
//    {
//        public static async Task HandleDeployCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
//        {
//            var data = callbackQuery.Data.Substring(7);
//            if (int.TryParse(data, out int projectIndex))
//            {
//                await ShowConfirmationKeyboardAsync(botClient, callbackQuery, projectIndex, cancellationToken);
//            }
//            else
//            {
//                await Paginator.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
//            }
//        }

//        public static async Task HandleConfirmYesCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
//        {
//            var projectIndex = int.Parse(callbackQuery.Data.Split('_')[2]);
//            var projects = await ProjectsCommand.GetJenkinsProjectsAsync();
//            var selectedProject = projects[projectIndex];

//            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
//            await DeployCommand.ExecuteAsync(botClient, callbackQuery.Message, selectedProject, cancellationToken);
//        }

//        public static async Task HandleConfirmNoCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
//        {
//            await botClient.SendTextMessageAsync(
//                chatId: callbackQuery.Message.Chat.Id,
//                text: "Yêu cầu /deploy của bạn đã bị hủy.",
//                replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Bắt đầu lại", "start_again")),
//                cancellationToken: cancellationToken);

//            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
//        }

//        private static async Task ShowConfirmationKeyboardAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, int projectIndex, CancellationToken cancellationToken)
//        {
//            var projects = await ProjectsCommand.GetJenkinsProjectsAsync();
//            var project = projects[projectIndex];

//            var confirmationKeyboard = new InlineKeyboardMarkup(new[]
//            {
//                InlineKeyboardButton.WithCallbackData("Yes", $"confirm_yes_{projectIndex}"),
//                InlineKeyboardButton.WithCallbackData("No", "confirm_no")
//            });

//            await botClient.EditMessageTextAsync(
//                chatId: callbackQuery.Message.Chat.Id,
//                messageId: callbackQuery.Message.MessageId,
//                text: $"Bạn đã chọn {project}. Bạn có muốn xác nhận triển khai không?",
//                replyMarkup: confirmationKeyboard,
//                cancellationToken: cancellationToken);
//        }
//    }
//}
