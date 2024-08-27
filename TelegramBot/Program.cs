using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using TelegramBot.Commands;
using TelegramBot.Utilities.DeployUtilities;
using TelegramBot.Utilities;
using dotenv.net;
using System.Collections.Concurrent;
using Npgsql;

namespace TelegramBot
{
    public class Program
    {
        public static ITelegramBotClient botClient;
        public static Dictionary<long, bool> feedbackState = new Dictionary<long, bool>();
        public static ConcurrentDictionary<long, string> schedulingState = new ConcurrentDictionary<long, string>();
        public static string connectionString { get; private set; }
        public static string botToken { get; private set; }

        public static async Task Main()
        {
            var envVars = DotEnv.Read(options: new DotEnvOptions(probeForEnv: true));
            botToken = envVars["TELEGRAM_BOT_TOKEN"];
            connectionString = envVars["DATABASE_CONNECTION_STRING"];

            botClient = new TelegramBotClient(botToken);
            await MenuCommand.SetBotCommandsAsync(botClient);
            var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };
            botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, receiverOptions);

            ScheduledJobManager.Initialize();

            Console.WriteLine("Bot started. Press any key to exit.");
            Console.ReadKey();
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                await HandleMessageAsync(update.Message, cancellationToken);
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                await HandleCallbackQueryAsync(update.CallbackQuery, cancellationToken);
            }
        }

        private static async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
        {
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

            if (feedbackState.TryGetValue(chatId, out bool isFeedback) && isFeedback)
            {
                feedbackState[chatId] = false;
                await FeedbackCommand.HandleFeedbackResponseAsync(botClient, message, cancellationToken);
                return;
            }

            if (schedulingState.TryGetValue(message.Chat.Id, out _))
            {
                await DeployCommand.HandleScheduleTimeInputAsync(botClient, message, cancellationToken);
                return;
            }

            if (text.StartsWith("/"))
            {
                var command = text.Split(' ')[0].ToLower();
                switch (command)
                {
                    case "/start":
                        await StartCommand.ExecuteAsync(botClient, message, cancellationToken);
                        break;
                    case "/deploy":
                        await ShowProjectsKeyboard(chatId, cancellationToken);
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
                    case "/projects":
                        await ProjectsCommand.ExecuteAsync(botClient, message, cancellationToken);
                        break;
                    case "/feedback":
                        feedbackState[chatId] = true;
                        await FeedbackCommand.ExecuteAsync(botClient, message, cancellationToken);
                        break;
                }
            }
        }

        private static async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;
            var data = callbackQuery.Data;

            if (data.StartsWith("deploy_"))
            {
                await HandleDeployCallback(callbackQuery, cancellationToken);
            }
            else if (data.StartsWith("page_"))
            {
                await DeployCommand.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
            else if (data == "start_again")
            {
                await StartCommand.ExecuteAsync(botClient, callbackQuery.Message, cancellationToken);
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
            }

            else if (data.StartsWith("schedule_job_"))
            {
                await DeployCommand.HandleScheduleJobAsync(botClient, callbackQuery, cancellationToken);
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
                var folderId = data.Substring(7);
                if (FolderKeyboardManager.folderPathMap.TryGetValue(folderId, out string folderPath))
                {
                    await DeployCommand.ExecuteAsync(botClient, callbackQuery.Message, folderPath, cancellationToken);
                }
            }

            //Job
            else if (data == "search")
            {
                await JobFinder.HandleSearchCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data == "back_to_folder")
            {
                await ShowProjectsKeyboard(chatId, cancellationToken);
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

        private static async Task HandleDeployCallback(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var data = callbackQuery.Data.Substring(7);
            if (int.TryParse(data, out int projectIndex))
            {
                await DeployConfirmation.DeployConfirmationKeyboard(botClient, callbackQuery, projectIndex, cancellationToken);
            }
            else
            {
                await DeployCommand.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
        }

        private static async Task ShowProjectsKeyboard(long chatId, CancellationToken cancellationToken)
        {
            var projects = await ProjectsCommand.GetJenkinsProjectsAsync();
            FolderPaginator.chatState[chatId] = projects;
            await FolderPaginator.ShowFoldersPage(botClient, chatId, projects, 0, cancellationToken);
        }

        private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }
    }
}