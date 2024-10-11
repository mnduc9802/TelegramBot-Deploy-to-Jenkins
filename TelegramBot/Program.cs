using System.Net.Sockets;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using TelegramBot.Commands.Major.Deploy;
using TelegramBot.Commands.Major.Project;
using TelegramBot.Commands.Minor;
using TelegramBot.Services;
using TelegramBot.Utilities.Deploy;
using TelegramBot.Utilities.Environment;

namespace TelegramBot
{
    public class Program
    {
        #region Fields and Properties
        public static ITelegramBotClient botClient;
        public static string? connectionString { get; private set; }
        public static string? botToken { get; private set; }
        #endregion

        #region Main Method
        public static async Task Main()
        {
            try
            {
                InitializeServices();
                await StartBot();
                await RunBot();
            }
            catch (Exception ex)
            {
                HandleCriticalError(ex);
            }
        }
        #endregion

        #region Initialization
        private static void InitializeServices()
        {
            LoggerService.Initialize();
            LoggerService.LogInformation($"Starting Telegram Bot... Log file location: {LoggerService.LogFilePath}");

            string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
            botToken = EnvironmentVariableLoader.GetTelegramBotToken();
            connectionString = EnvironmentVariableLoader.GetDatabaseConnectionString();

            if (string.IsNullOrEmpty(botToken))
            {
                LoggerService.LogError(new Exception("Bot token is missing"), "Bot token is not configured");
                throw new InvalidOperationException("Bot token is missing");
            }
        }

        private static async Task StartBot()
        {
            botClient = new TelegramBotClient(botToken);

            try
            {
                var me = await botClient.GetMeAsync();
                LoggerService.LogInformation($"Bot started successfully. Bot Username: {me.Username}");
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, "Failed to initialize bot client");
                throw;
            }
        }

        private static async Task RunBot()
        {
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
        #endregion

        #region Update Handling
        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                LogUpdateInfo(update);

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

        private static void LogUpdateInfo(Update update)
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
        }
        #endregion

        #region Message Handling
        private static async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                LogMessageInfo(message);

                var chatId = message.Chat.Id;
                var text = message.Text;

                if (string.IsNullOrEmpty(text))
                    return;

                await ProcessMessageText(message, text, chatId, cancellationToken);
            }
            catch (Exception ex)
            {
                await HandleMessageError(ex, message, cancellationToken);
            }
        }

        private static void LogMessageInfo(Message message)
        {
            LoggerService.LogInformation(
            "Handling message. MessageId: {MessageId}, ChatId: {ChatId}, Username: {Username}, Text: {Text}",
            message.MessageId, message.Chat.Id, message.From?.Username ?? "Unknown", message.Text ?? "No text");
        }

        private static async Task ProcessMessageText(Message message, string text, long chatId, CancellationToken cancellationToken)
        {
            var botUsername = (await botClient.GetMeAsync(cancellationToken)).Username;
            text = NormalizeBotCommand(text, botUsername);

            if (message.Chat.Type != ChatType.Private && !text.Contains("@" + botUsername))
            {
                return;
            }

            await HandleSpecialStates(botClient, message, chatId, cancellationToken);
            await HandleBotCommands(botClient, message, text, cancellationToken);
        }

        private static string NormalizeBotCommand(string text, string botUsername)
        {
            var commandParts = text.Split('@');
            if (commandParts.Length == 2 && commandParts[1].Equals(botUsername, StringComparison.OrdinalIgnoreCase))
            {
                return commandParts[0];
            }
            return text;
        }

        private static async Task HandleSpecialStates(ITelegramBotClient botClient, Message message, long chatId, CancellationToken cancellationToken)
        {
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
                await HandleScheduleStates(botClient, message, state, cancellationToken);
                return;
            }
        }

        private static async Task HandleScheduleStates(ITelegramBotClient botClient, Message message, string state, CancellationToken cancellationToken)
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
        }

        private static async Task HandleBotCommands(ITelegramBotClient botClient, Message message, string text, CancellationToken cancellationToken)
        {
            if (text.StartsWith("/"))
            {
                var command = text.Split(' ')[0].ToLower();
                switch (command)
                {
                    case "/hello":
                        await HelloCommand.ExecuteAsync(botClient, message, cancellationToken);
                        break;
                    case "/projects":
                        await ProjectCommand.ExecuteAsync(botClient, message, cancellationToken);
                        break;
                    case "/deploy":
                        await DeployCommand.ShowProjectsKeyboard(botClient, message.Chat.Id, message.From.Id, cancellationToken);
                        break;
                    case "/myinfo":
                        await MyInfoCommand.ExecuteAsync(botClient, message, cancellationToken);
                        break;
                    case "/clear":
                        await ClearCommand.ClearConfirmationKeyboard(botClient, message.Chat.Id, cancellationToken);
                        break;
                    case "/status":
                        await StatusCommand.ExecuteAsync(botClient, message, cancellationToken);
                        break;
                    case "/notify":
                        await NotifyCommand.ExecuteAsync(botClient, message, cancellationToken);
                        break;
                    case "/feedback":
                        FeedbackCommand.feedbackState[message.Chat.Id] = true;
                        await FeedbackCommand.ExecuteAsync(botClient, message, cancellationToken);
                        break;
                    case "/help":
                        await HelpCommand.ExecuteAsync(botClient, message, cancellationToken);
                        break;
                }
            }
        }

        private static async Task HandleMessageError(Exception ex, Message message, CancellationToken cancellationToken)
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
        #endregion

        #region Callback Query Handling
        private static async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
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
                await HandleCallbackQueryError(ex, callbackQuery, cancellationToken);
            }
        }

        private static async Task ProcessCallbackData(ITelegramBotClient botClient, CallbackQuery callbackQuery, string data, long chatId, int messageId, CancellationToken cancellationToken)
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
            else if (data == "foldersearch")
            {
                await FolderFinder.HandleSearchCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data.StartsWith("folder_"))
            {
                await HandleFolderCallback(botClient, callbackQuery, data, cancellationToken);
            }
            else if (data.StartsWith("enter_version_"))
            {
                await HandleEnterVersionCallback(botClient, callbackQuery, data, cancellationToken);
            }

            //Job
            else if (data == "search")
            {
                await JobFinder.HandleSearchCallback(botClient, callbackQuery, cancellationToken);
            }
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

        private static async Task HandleFolderCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
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

        private static async Task HandleEnterVersionCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
        {
            var jobUrlId = data.Substring("enter_version_".Length);
            DeployCommand.versionInputState[callbackQuery.Message.Chat.Id] = jobUrlId;
            await botClient.SendTextMessageAsync(
                callbackQuery.Message.Chat.Id,
                "Vui lòng nhập tham số VERSION:",
                cancellationToken: cancellationToken);
            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
        }

        private static async Task HandleBackToFolderCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await DeployCommand.ShowProjectsKeyboard(botClient, callbackQuery.Message.Chat.Id, callbackQuery.From.Id, cancellationToken);
            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
        }

        private static async Task HandleBackToJobsCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
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

        private static async Task HandleCallbackQueryError(Exception ex, CallbackQuery callbackQuery, CancellationToken cancellationToken)
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
        #endregion

        #region Error Handling
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

        private static void HandleCriticalError(Exception ex)
        {
            Console.WriteLine($"Critical error during bot startup: {ex.Message}");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
        #endregion
    }
}