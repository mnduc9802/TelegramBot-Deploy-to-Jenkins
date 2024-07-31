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

            var me = await botClient.GetMeAsync();
            Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");

            // Thiết lập các lệnh cho bot
            await MenuCommand.SetBotCommandsAsync(botClient);

            var cancellationTokenSource = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cancellationTokenSource.Token
            );

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();

            cancellationTokenSource.Cancel();
        }

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                var chatId = update.Message.Chat.Id;
                var text = update.Message.Text;

                if (feedbackState.ContainsKey(chatId) && feedbackState[chatId])
                {
                    feedbackState[chatId] = false;
                    await FeedbackCommand.HandleFeedbackResponseAsync(botClient, update.Message, cancellationToken);
                }
                else
                {
                    switch (text)
                    {
                        case "/start":
                            await StartCommand.ExecuteAsync(botClient, update.Message, cancellationToken);
                            break;
                        case "/deploy":
                            var projects = await ProjectsCommand.GetJenkinsProjectsAsync();
                            var projectButtons = new List<InlineKeyboardButton[]>();

                            for (int i = 0; i < projects.Count; i++)
                            {
                                projectButtons.Add(new[]
                                {
                            InlineKeyboardButton.WithCallbackData(projects[i], $"deploy_{i}")
                        });
                            }

                            var projectKeyboard = new InlineKeyboardMarkup(projectButtons);

                            await botClient.SendTextMessageAsync(
                                chatId: chatId,
                                text: "Danh sách các dự án hiện tại:",
                                replyMarkup: projectKeyboard,
                                cancellationToken: cancellationToken);
                            break;
                        case "/clear":
                            await ClearCommand.RequestConfirmationAsync(botClient, chatId, cancellationToken);
                            break;
                        case "/help":
                            await HelpCommand.ExecuteAsync(botClient, update.Message, cancellationToken);
                            break;
                        case "/status":
                            await StatusCommand.ExecuteAsync(botClient, update.Message, cancellationToken);
                            break;
                        case "/projects":
                            await ProjectsCommand.ExecuteAsync(botClient, update.Message, cancellationToken);
                            break;
                        case "/feedback":
                            feedbackState[chatId] = true;
                            await FeedbackCommand.ExecuteAsync(botClient, update.Message, cancellationToken);
                            break;
                    }
                }
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                var callbackQuery = update.CallbackQuery;
                var chatId = callbackQuery.Message.Chat.Id;
                var callbackData = callbackQuery.Data;

                if (callbackData.StartsWith("deploy_"))
                {
                    var projectPath = callbackData.Substring(7); // Lấy phần còn lại sau "deploy_"

                    // Kiểm tra xem projectPath có chứa "/" không để xác định đây là dự án trong thư mục
                    if (projectPath.Contains("/"))
                    {
                        // Xử lý cho dự án trong thư mục
                        var folderPath = projectPath.Split('/')[0];
                        var jobName = projectPath.Split('/')[1];
                        await DeployCommand.ExecuteAsync(botClient, callbackQuery.Message, projectPath, cancellationToken);
                    }
                    else
                    {
                        // Xử lý cho dự án đơn lẻ (giữ nguyên logic cũ)
                        if (!int.TryParse(projectPath, out int projectIndex))
                        {
                            Console.WriteLine($"Invalid data received: {callbackData}");
                            await botClient.SendTextMessageAsync(
                                chatId: callbackQuery.Message.Chat.Id,
                                text: "Dữ liệu không hợp lệ. Vui lòng thử lại.",
                                cancellationToken: cancellationToken);
                            return;
                        }

                        var projects = await ProjectsCommand.GetJenkinsProjectsAsync();
                        if (projectIndex < 0 || projectIndex >= projects.Count)
                        {
                            Console.WriteLine($"Invalid project index: {projectIndex}");
                            await botClient.SendTextMessageAsync(
                                chatId: callbackQuery.Message.Chat.Id,
                                text: "Dự án không hợp lệ. Vui lòng thử lại.",
                                cancellationToken: cancellationToken);
                            return;
                        }

                        string project = projects[projectIndex];

                        // Tiếp tục với logic xác nhận triển khai
                        var confirmationKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            InlineKeyboardButton.WithCallbackData("Yes", $"confirm_yes_{projectPath}"),
                            InlineKeyboardButton.WithCallbackData("No", "confirm_no")
                        });

                        await botClient.EditMessageTextAsync(
                            chatId: callbackQuery.Message.Chat.Id,
                            messageId: callbackQuery.Message.MessageId,
                            text: $"Bạn đã chọn {project}. Bạn có muốn xác nhận triển khai không?",
                            replyMarkup: confirmationKeyboard,
                            cancellationToken: cancellationToken);
                    }
                }
                else if (callbackData.StartsWith("confirm_yes_"))
                {
                    if (!int.TryParse(callbackData.Split('_')[2], out int index))
                    {
                        Console.WriteLine($"Invalid confirmation data received: {callbackData}"); // Log lỗi
                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Dữ liệu xác nhận không hợp lệ. Vui lòng thử lại.",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    var selectedProjects = await ProjectsCommand.GetJenkinsProjectsAsync();
                    if (index < 0 || index >= selectedProjects.Count)
                    {
                        Console.WriteLine($"Invalid confirmation project index: {index}"); // Log lỗi
                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Dự án không hợp lệ. Vui lòng thử lại.",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    string selectedProject = selectedProjects[index];

                    await botClient.DeleteMessageAsync(
                        chatId: chatId,
                        messageId: callbackQuery.Message.MessageId,
                        cancellationToken: cancellationToken);

                    await DeployCommand.ExecuteAsync(botClient, callbackQuery.Message, selectedProject, cancellationToken);
                }
                else if (callbackData == "confirm_no")
                {
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Yêu cầu của bạn đã bị hủy.",
                        replyMarkup: new InlineKeyboardMarkup(new[]
                        {
                    InlineKeyboardButton.WithCallbackData("Bắt đầu lại", "start_again")
                        }),
                        cancellationToken: cancellationToken);

                    await botClient.DeleteMessageAsync(
                        chatId: chatId,
                        messageId: callbackQuery.Message.MessageId,
                        cancellationToken: cancellationToken);
                }
                else if (callbackData == "start_again")
                {
                    await StartCommand.ExecuteAsync(botClient, callbackQuery.Message, cancellationToken);

                    await botClient.DeleteMessageAsync(
                        chatId: chatId,
                        messageId: callbackQuery.Message.MessageId,
                        cancellationToken: cancellationToken);
                }

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
                await ClearCommand.HandleClearCallbackAsync(botClient, callbackQuery, cancellationToken);
            }
        }
        //Có thể tối ưu đoạn này -mnduc9802
        private static async Task HandleDeployConfirmation(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var projectPath = callbackQuery.Data.Split('_')[2];

            await botClient.DeleteMessageAsync(
                chatId: callbackQuery.Message.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                cancellationToken: cancellationToken);

            await DeployCommand.ExecuteAsync(botClient, callbackQuery.Message, projectPath, cancellationToken);
        }

        public static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
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
