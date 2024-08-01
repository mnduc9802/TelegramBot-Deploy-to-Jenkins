using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using TelegramBot.Commands;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

            Console.WriteLine("==========================================");
            Console.WriteLine("    TELEGRAM DEPLOY BOT by mnduc9802   ");
            Console.WriteLine("Bot started. Press any key to exit.");
            Console.WriteLine("==========================================");
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

            if (feedbackState.TryGetValue(chatId, out bool isFeedback) && isFeedback)
            {
                feedbackState[chatId] = false;
                await FeedbackCommand.HandleFeedbackResponseAsync(botClient, message, cancellationToken);
                return;
            }

            switch (text)
            {
                case "/start":
                    await StartCommand.ExecuteAsync(botClient, message, cancellationToken);
                    break;
                case "/deploy":
                    await ShowProjectsKeyboard(chatId, cancellationToken);
                    break;
                case "/clear":
                    await ClearCommand.RequestConfirmationAsync(botClient, chatId, cancellationToken);
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

        private static async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var data = callbackQuery.Data;

            if (data.StartsWith("deploy_"))
            {
                await HandleDeployCallback(callbackQuery, cancellationToken);
            }
            else if (data.StartsWith("confirm_yes_"))
            {
                await HandleConfirmYesCallback(callbackQuery, cancellationToken);
            }
            else if (data == "confirm_no")
            {
                await HandleConfirmNoCallback(callbackQuery, cancellationToken);
            }
            else if (data == "start_again")
            {
                await StartCommand.ExecuteAsync(botClient, callbackQuery.Message, cancellationToken);
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            await ClearCommand.HandleClearCallbackAsync(botClient, callbackQuery, cancellationToken);
        }

        private static async Task HandleDeployCallback(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var data = callbackQuery.Data.Substring(7);
            if (int.TryParse(data, out int projectIndex))
            {
                await ShowConfirmationKeyboard(callbackQuery, projectIndex, cancellationToken);
            }
            else
            {
                await DeployCommand.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
        }

        private static async Task HandleConfirmYesCallback(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var projectIndex = int.Parse(callbackQuery.Data.Split('_')[2]);
            var projects = await ProjectsCommand.GetJenkinsProjectsAsync();
            var selectedProject = projects[projectIndex];

            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
            await DeployCommand.ExecuteAsync(botClient, callbackQuery.Message, selectedProject, cancellationToken);
        }

        private static async Task HandleConfirmNoCallback(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(
                chatId: callbackQuery.Message.Chat.Id,
                text: "Yêu cầu /deploy của bạn đã bị hủy.",
                replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Bắt đầu lại", "start_again")),
                cancellationToken: cancellationToken);

            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
        }

        private static async Task ShowProjectsKeyboard(long chatId, CancellationToken cancellationToken)
        {
            var projects = await ProjectsCommand.GetJenkinsProjectsAsync();
            var projectButtons = projects.Select((p, i) => new[] { InlineKeyboardButton.WithCallbackData(p, $"deploy_{i}") }).ToList();
            var projectKeyboard = new InlineKeyboardMarkup(projectButtons);

            await botClient.SendTextMessageAsync(chatId, "Danh sách các dự án hiện tại:", replyMarkup: projectKeyboard, cancellationToken: cancellationToken);
        }

        private static async Task ShowConfirmationKeyboard(CallbackQuery callbackQuery, int projectIndex, CancellationToken cancellationToken)
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
