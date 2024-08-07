using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using TelegramBot.Commands;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Utilities.DeployUtilities;

namespace TelegramBot
{
    public class Program
    {
        public static ITelegramBotClient botClient;
        public static Dictionary<long, bool> feedbackState = new Dictionary<long, bool>();

        public static async Task Main()
        {
            botClient = new TelegramBotClient("7463243734:AAEXs6bid2YewLvCx6iMxzEqRgW2UweCZX4");
            await MenuCommand.SetBotCommandsAsync(botClient);

            var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };
            botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, receiverOptions);

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

            if (message.ReplyToMessage?.Text == "Vui lòng nhập tên job bạn muốn tìm kiếm:")
            {
                await JobFinder.HandleSearchQuery(botClient, message, cancellationToken);
                return;
            }

            if (feedbackState.TryGetValue(chatId, out bool isFeedback) && isFeedback)
            {
                feedbackState[chatId] = false;
                await FeedbackCommand.HandleFeedbackResponseAsync(botClient, message, cancellationToken);
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
            var data = callbackQuery.Data;

            if (data.StartsWith("deploy_"))
            {
                await HandleDeployCallback(callbackQuery, cancellationToken);
            }
            else if (data.StartsWith("page_"))
            {
                await DeployCommand.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
            else if (data.StartsWith("confirm_folder_yes_"))
            {
                await DeployConfirmation.HandleConfirmFolderYesCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data == "confirm_folder_no")
            {
                await DeployConfirmation.HandleConfirmFolderNoCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data.StartsWith("confirm_job_yes_"))
            {
                await DeployConfirmation.HandleConfirmJobYesCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data == "confirm_job_no")
            {
                await DeployConfirmation.HandleConfirmJobNoCallback(botClient, callbackQuery, cancellationToken);
            }
            else if (data == "start_again")
            {
                await StartCommand.ExecuteAsync(botClient, callbackQuery.Message, cancellationToken);
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
            }
            else if (data == "search")
            {
                await JobFinder.HandleSearchCallback(botClient, callbackQuery, cancellationToken);
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            await ClearCommand.HandleClearCallbackAsync(botClient, callbackQuery, cancellationToken);
        }

        private static async Task HandleDeployCallback(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var data = callbackQuery.Data.Substring(7);
            if (int.TryParse(data, out int projectIndex))
            {
                await DeployConfirmation.FolderDeployConfirmationKeyboard(botClient, callbackQuery, projectIndex, cancellationToken);
            }
            else
            {
                await DeployCommand.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
        }

        private static async Task ShowProjectsKeyboard(long chatId, CancellationToken cancellationToken)
        {
            var projects = await ProjectsCommand.GetJenkinsProjectsAsync();
            var projectButtons = projects.Select((p, i) => new[] { InlineKeyboardButton.WithCallbackData(p, $"deploy_{i}") }).ToList();
            var projectKeyboard = new InlineKeyboardMarkup(projectButtons);

            await botClient.SendTextMessageAsync(chatId, "Danh sách các dự án hiện tại:", replyMarkup: projectKeyboard, cancellationToken: cancellationToken);
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
