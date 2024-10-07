using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using TelegramBot.Utilities.DeployUtilities;
using TelegramBot.Commands.MajorCommands.DeployCommand;
using TelegramBot.Commands.MajorCommands.ProjectCommand;
using TelegramBot.Commands.MinorCommands;
using TelegramBot.Utilities.EnvironmentUtilities;
using TelegramBot.Services;
using System.Net.Sockets;

namespace TelegramBot
{
    public class Program
    {
        public static ITelegramBotClient botClient;
        public static string? connectionString { get; private set; }
        public static string? botToken { get; private set; }

        public static async Task Main()
        {
            try
            {
                LoggerService.Initialize();
                LoggerService.LogInformation($"Starting Telegram Bot... Log file location: {LoggerService.LogFilePath}");

                string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
                botToken = EnvironmentVariableLoader.GetTelegramBotToken();
                connectionString = EnvironmentVariableLoader.GetDatabaseConnectionString();

                if (string.IsNullOrEmpty(botToken))
                {
                    LoggerService.LogError(new Exception("Bot token is missing"), "Bot token is not configured");
                    return;
                }

                botClient = new TelegramBotClient(botToken);

                try
                {
                    var me = await botClient.GetMeAsync();
                    LoggerService.LogInformation($"Bot started successfully. Bot Username: {me.Username}");
                }
                catch (Exception ex)
                {
                    LoggerService.LogError(ex, "Failed to initialize bot client");
                    return;
                }

                await MenuCommand.SetBotCommandsAsync(botClient);
                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = Array.Empty<UpdateType>(),
                    ThrowPendingUpdates = true
                };

                botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, receiverOptions);

                JobService.Initialize();
                LoggerService.LogInformation("Bot is running. Press any key to exit.");
                Console.WriteLine($"Bot is running. Logs are being written to: {LoggerService.LogFilePath}");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error during bot startup: {ex.Message}");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Message?.Chat == null && update.CallbackQuery?.Message?.Chat == null)
                {
                    LoggerService.LogWarning("Received update with no chat information. UpdateId: {UpdateId}", update.Id);
                    return;
                }

                var chatId = update.Message?.Chat.Id ?? update.CallbackQuery.Message.Chat.Id;
                var username = update.Message?.From?.Username ?? update.CallbackQuery?.From?.Username ?? "Unknown";
                var updateType = update.Type.ToString();

                LoggerService.LogInformation(
                    "Processing update. Type: {UpdateType}, UpdateId: {UpdateId}, ChatId: {ChatId}, Username: {Username}",
                    updateType, update.Id, chatId, username);

                if (update.Type == UpdateType.Message && update.Message?.Text != null)
                {
                    LoggerService.LogDebug("Received message: {Message}", update.Message.Text);
                    await HandleMessageAsync(update.Message, cancellationToken);
                }
                else if (update.Type == UpdateType.CallbackQuery)
                {
                    LoggerService.LogDebug("Received callback query: {CallbackData}", update.CallbackQuery.Data);
                    await HandleCallbackQueryAsync(update.CallbackQuery, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex,
                    "Error processing update. UpdateId: {UpdateId}, Type: {UpdateType}",
                    update.Id, update.Type);
            }
        }

        private static async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                LoggerService.LogInformation(
                "Handling message. MessageId: {MessageId}, ChatId: {ChatId}, Username: {Username}, Text: {Text}",
                message.MessageId, message.Chat.Id, message.From?.Username ?? "Unknown", message.Text ?? "No text");

                var chatId = message.Chat.Id;
                var text = message.Text;

                if (string.IsNullOrEmpty(text))
                    return;

                var botUsername = (await botClient.GetMeAsync(cancellationToken)).Username;
                var commandParts = text.Split('@');

                if (commandParts.Length == 2 && commandParts[1].Equals(botUsername, StringComparison.OrdinalIgnoreCase))
                {
                    text = commandParts[0];
                }
                else if (message.Chat.Type != ChatType.Private && !text.Contains("@" + botUsername))
                {
                    return;
                }

                await FolderFinder.HandleSearchQuery(botClient, message, cancellationToken);
                await JobFinder.HandleSearchQuery(botClient, message, cancellationToken);

                if (FeedbackCommand.feedbackState.TryGetValue(chatId, out bool isFeedback) && isFeedback)
                {
                    FeedbackCommand.feedbackState[chatId] = false;
                    await FeedbackCommand.HandleFeedbackResponseAsync(botClient, message, cancellationToken);
                    return;
                }

                if (DeployCommand.versionInputState.TryGetValue(message.Chat.Id, out string? jobUrl))
                {
                    await DeployCommand.HandleVersionInputAsync(botClient, message, jobUrl, cancellationToken);
                    return;
                }

                if (ScheduleJob.schedulingState.TryGetValue(message.Chat.Id, out string? state))
                {
                    if (state.StartsWith("schedule_time_"))
                    {
                        await ScheduleJob.HandleScheduleTimeInputAsync(botClient, message, cancellationToken);
                    }
                    else if (state.StartsWith("schedule_version_"))
                    {
                        await ScheduleJob.HandleScheduleParameterInputAsync(botClient, message, cancellationToken);
                    }
                    else if (state.StartsWith("edit_"))
                    {
                        await ListScheduleJob.HandleEditJobTimeInputAsync(botClient, message, cancellationToken);
                    }
                    return;
                }

                if (text.StartsWith("/"))
                {
                    var command = text.Split(' ')[0].ToLower();
                    switch (command)
                    {
                        case "/hello":
                            await HelloCommand.ExecuteAsync(botClient, message, cancellationToken);
                            break;
                        case "/notify":
                            await NotifyCommand.ExecuteAsync(botClient, message, cancellationToken);
                            break;
                        case "/deploy":
                            await DeployCommand.ShowProjectsKeyboard(botClient, chatId, message.From.Id, cancellationToken);
                            break;
                        case "/projects":
                            await ProjectCommand.ExecuteAsync(botClient, message, cancellationToken);
                            break;
                        case "/clear":
                            await ClearCommand.ClearConfirmationKeyboard(botClient, chatId, cancellationToken);
                            break;
                        case "/help":
                            await HelpCommand.ExecuteAsync(botClient, message, cancellationToken);
                            break;
                        case "/status":
                            await StatusCommand.ExecuteAsync(botClient, message, cancellationToken);
                            break;
                        case "/feedback":
                            FeedbackCommand.feedbackState[chatId] = true;
                            await FeedbackCommand.ExecuteAsync(botClient, message, cancellationToken);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex,
                    "Error handling message. MessageId: {MessageId}, ChatId: {ChatId}",
                    message.MessageId, message.Chat.Id);
                try
                {
                    await botClient.SendTextMessageAsync(
                        message.Chat.Id,
                        "Xin lỗi, đã xảy ra lỗi khi xử lý yêu cầu của bạn.",
                        cancellationToken: cancellationToken);
                }
                catch (Exception sendEx)
                {
                    LoggerService.LogError(sendEx,
                        "Failed to send error message to user. ChatId: {ChatId}",
                        message.Chat.Id);
                }
            }
        }

        private static async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            try
            {
                var chatId = callbackQuery.Message.Chat.Id;
                var messageId = callbackQuery.Message.MessageId;
                var data = callbackQuery.Data;

                Console.WriteLine($"Received callback query data: {data}");

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
                else if (data == "foldersearch")
                {
                    await FolderFinder.HandleSearchCallback(botClient, callbackQuery, cancellationToken);
                }
                else if (data.StartsWith("folder_"))
                {
                    const int FOLDER_ID_START_INDEX = 7;
                    var folderId = data.Substring(FOLDER_ID_START_INDEX);
                    if (FolderKeyboardManager.folderPathMap.TryGetValue(folderId, out string? folderPath))
                    {
                        await DeployCommand.ExecuteAsync(botClient, callbackQuery.Message, folderPath, cancellationToken);
                        await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
                    }
                }
                else if (data.StartsWith("enter_version_"))
                {
                    var jobUrlId = data.Substring("enter_version_".Length);
                    DeployCommand.versionInputState[callbackQuery.Message.Chat.Id] = jobUrlId;
                    await botClient.SendTextMessageAsync(
                        callbackQuery.Message.Chat.Id,
                        "Vui lòng nhập tham số VERSION:",
                        cancellationToken: cancellationToken);
                    await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
                }

                //Job
                else if (data == "search")
                {
                    await JobFinder.HandleSearchCallback(botClient, callbackQuery, cancellationToken);
                }
                else if (data == "back_to_folder")
                {
                    await DeployCommand.ShowProjectsKeyboard(botClient, chatId, callbackQuery.From.Id, cancellationToken);
                    await botClient.DeleteMessageAsync(chatId, messageId, cancellationToken);
                }
                else if (data == "back_to_jobs")
                {
                    if (JobPaginator.chatState.TryGetValue(chatId, out var state))
                    {
                        await JobPaginator.ShowJobsPage(botClient, chatId, state.Jobs, 0, state.ProjectPath, cancellationToken);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "Không thể tìm thấy thông tin trạng thái. Vui lòng thử lại từ đầu.", cancellationToken: cancellationToken);
                    }
                    await botClient.DeleteMessageAsync(chatId, messageId, cancellationToken);
                }

                //Scheduled Job
                else if (data.StartsWith("schedule_job_"))
                {
                    await ScheduleJob.HandleScheduleJobAsync(botClient, callbackQuery, cancellationToken);
                }

                else if (data.StartsWith("show_scheduled_jobs"))
                {
                    await ProjectCommand.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
                }

                else if (data.StartsWith("edit_job_"))
                {
                    await ProjectCommand.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
                }

                else if (data.StartsWith("delete_job_"))
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

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
                await ClearCommand.HandleClearCallbackAsync(botClient, callbackQuery, cancellationToken);
            }
            catch (Exception ex)
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

        private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException =>
                    $"Telegram API Error [{apiRequestException.ErrorCode}]: {apiRequestException.Message}",
                HttpRequestException httpRequestException =>
                    $"HTTP Request Error: {httpRequestException.Message}",
                SocketException socketException =>
                    $"Network Error [{socketException.SocketErrorCode}]: {socketException.Message}",
                TaskCanceledException =>
                    "Request cancelled",
                _ => exception.ToString()
            };

            LoggerService.LogError(exception, "Polling error: {ErrorMessage}", errorMessage);

            if (exception is HttpRequestException || exception is SocketException)
            {
                LoggerService.LogInformation("Will attempt to reconnect in 5 seconds...");
            }

            return Task.CompletedTask;
        }
    }
}