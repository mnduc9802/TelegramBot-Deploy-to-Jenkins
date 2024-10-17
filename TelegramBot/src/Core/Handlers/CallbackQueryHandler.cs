using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Commands.Major.Deploy;
using TelegramBot.Commands.Major.Project;
using TelegramBot.Commands.Minor;
using TelegramBot.Services;
using TelegramBot.Utilities.Deploy.General;
using TelegramBot.Utilities.Deploy.FolderUtilities;
using TelegramBot.Utilities.Deploy.JobUtilities;

namespace TelegramBot.Core.Handlers
{
    public class CallbackQueryHandler
    {
        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            try
            {
                var chatId = callbackQuery.Message.Chat.Id;
                var messageId = callbackQuery.Message.MessageId;
                var data = callbackQuery.Data;

                LoggerService.LogDebug("Received callback query. Data: {Data}, ChatId: {ChatId}", data, chatId);

                await ProcessCallbackData(botClient, callbackQuery, data, chatId, messageId, cancellationToken);

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
                await ClearCommand.HandleClearCallbackAsync(botClient, callbackQuery, cancellationToken);
            }
            catch (Exception ex)
            {
                await HandleCallbackQueryError(botClient, ex, callbackQuery, cancellationToken);
            }
        }

        public static async Task ProcessCallbackData(ITelegramBotClient botClient, CallbackQuery callbackQuery, string data, long chatId, int messageId, CancellationToken cancellationToken)
        {

            LoggerService.LogDebug("Received callback query. Data: {Data}, ChatId: {ChatId}", data, chatId);

            if (data.StartsWith("deploy_"))
            {
                await DeployCommand.HandleDeployCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data.StartsWith("page_"))
            {
                await DeployCommand.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
            else if (data == "show_projects")
            {
                await ProjectCommand.ShowProjects(botClient, chatId, callbackQuery.From.Id, cancellationToken);
            }

            //Folder
            else if (data.StartsWith("folderpage_"))
            {
                await FolderPaginator.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
            else if (data == "search")
            {
                await CombinedSearchUtility.HandleSearchCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data.StartsWith("folder_"))
            {
                await HandleFolderCallback(botClient, callbackQuery, data, cancellationToken);
            }
            else if (data.StartsWith("enter_version_"))
            {
                await HandleEnterVersionCallback(botClient, callbackQuery, data, cancellationToken);
            }

            ////Job
            //else if (data == "combined_search")
            //{
            //    await CombinedSearchHandler.HandleSearchCallback(botClient, callbackQuery, cancellationToken);
            //}
            else if (data == "back_to_folder")
            {
                await HandleBackToFolderCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data == "back_to_jobs")
            {
                await HandleBackToJobsCallback(botClient, callbackQuery, cancellationToken);
            }

            //Scheduled Job
            else if (data.StartsWith("schedule_job_"))
            {
                await ScheduleJob.HandleScheduleJobAsync(botClient, callbackQuery, cancellationToken);
            }
            else if (data.StartsWith("show_scheduled_jobs") || data.StartsWith("edit_job_") || data.StartsWith("delete_job_"))
            {
                await ProjectCommand.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }

            //Confirmation Folder/Job
            else if (data.StartsWith("confirm_yes_"))
            {
                await DeployConfirmation.HandleConfirmYesCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data == "confirm_no")
            {
                await DeployConfirmation.HandleConfirmNoCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data.StartsWith("confirm_job_yes_"))
            {
                await DeployConfirmation.HandleConfirmJobYesCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data == "confirm_job_no")
            {
                await DeployConfirmation.HandleConfirmJobNoCallback(botClient, callbackQuery, cancellationToken);
            }
        }

        public static async Task HandleFolderCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
        {
            const int FOLDER_ID_START_INDEX = 7;
            var folderId = data.Substring(FOLDER_ID_START_INDEX);
            var userId = callbackQuery.From?.Id ?? throw new ArgumentNullException(nameof(callbackQuery.From.Id));
            LoggerService.LogInformation("Processing folder callback. UserId: {UserId}, FolderId: {FolderId}", userId, folderId);

            if (FolderKeyboardManager.folderPathMap.TryGetValue(folderId, out string? folderPath))
            {
                var messageFromCallbackQuery = callbackQuery.Message;
                var newMessage = new Message
                {
                    MessageId = messageFromCallbackQuery.MessageId,
                    From = callbackQuery.From,
                    Chat = messageFromCallbackQuery.Chat,
                    Date = messageFromCallbackQuery.Date
                };

                LoggerService.LogDebug("Created new message from callback. UserId: {UserId}, MessageId: {MessageId}", newMessage.From?.Id, newMessage.MessageId);
                await DeployCommand.ExecuteAsync(botClient, newMessage, folderPath, cancellationToken);
                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
            }
        }

        public static async Task HandleEnterVersionCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
        {
            var jobUrlId = data.Substring("enter_version_".Length);
            DeployCommand.versionInputState[callbackQuery.Message.Chat.Id] = jobUrlId;
            await botClient.SendTextMessageAsync(
                callbackQuery.Message.Chat.Id,
                "Vui lòng nhập tham số VERSION:",
                cancellationToken: cancellationToken);
            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
        }

        public static async Task HandleBackToFolderCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await DeployCommand.ShowProjectsKeyboard(botClient, callbackQuery.Message.Chat.Id, callbackQuery.From.Id, cancellationToken);
            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
        }

        public static async Task HandleBackToJobsCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            if (JobPaginator.chatState.TryGetValue(callbackQuery.Message.Chat.Id, out var state))
            {
                await JobPaginator.ShowJobsPage(botClient, callbackQuery.Message.Chat.Id, state.Jobs, 0, state.ProjectPath, cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Không thể tìm thấy thông tin trạng thái. Vui lòng thử lại từ đầu.", cancellationToken: cancellationToken);
            }
            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
        }

        public static async Task HandleCallbackQueryError(ITelegramBotClient botClient, Exception ex, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            LoggerService.LogError(ex, $"Error handling callback query from chat {callbackQuery.Message.Chat.Id}");
            try
            {
                await botClient.AnswerCallbackQueryAsync(
                    callbackQuery.Id,
                    "Xin lỗi, đã xảy ra lỗi khi xử lý yêu cầu của bạn.",
                    cancellationToken: cancellationToken);
            }
            catch (Exception sendEx)
            {
                LoggerService.LogError(sendEx, "Failed to send error callback answer to user");
            }
        }
    }
}