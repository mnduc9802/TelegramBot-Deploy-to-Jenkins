using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using TelegramBot.Commands;

class Program
{
    static ITelegramBotClient botClient;
    static List<string> projects = new List<string> { "Project A", "Project B", "Project C" };
    static Dictionary<long, bool> feedbackState = new Dictionary<long, bool>();

    static async Task Main()
    {
        botClient = new TelegramBotClient("7463243734:AAEXs6bid2YewLvCx6iMxzEqRgW2UweCZX4");

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");

        // Thiết lập các lệnh cho bot
        await SetBotCommandsAsync(botClient);

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

    static async Task SetBotCommandsAsync(ITelegramBotClient botClient)
    {
        var commands = new[]
        {
            new BotCommand { Command = "start", Description = "Bắt đầu sử dụng bot" },
            new BotCommand { Command = "clear", Description = "Xóa tất cả tin nhắn" },
            new BotCommand { Command = "help", Description = "Hiển thị trợ giúp" },
            new BotCommand { Command = "status", Description = "Hiển thị trạng thái" },
            new BotCommand { Command = "projects", Description = "Danh sách các dự án" },
            new BotCommand { Command = "feedback", Description = "Gửi phản hồi" }
        };

        await botClient.SetMyCommandsAsync(commands);
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
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
                    case "/clear":
                        await ClearCommand.ExecuteAsync(botClient, chatId, cancellationToken);
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

            switch (callbackData)
            {
                case "deploy":
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

                case "projects":
                    await ProjectsCommand.ExecuteAsync(botClient, callbackQuery.Message, cancellationToken);
                    break;

                case "status":
                    await StatusCommand.ExecuteAsync(botClient, callbackQuery.Message, cancellationToken);
                    break;

                case "help":
                    await HelpCommand.ExecuteAsync(botClient, callbackQuery.Message, cancellationToken);
                    break;

                case string data when data.StartsWith("deploy_"):
                    int projectIndex = int.Parse(data.Split('_')[1]);
                    string project = projects[projectIndex];

                    var confirmationKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Yes", $"confirm_yes_{projectIndex}"),
                        InlineKeyboardButton.WithCallbackData("No", "confirm_no")
                    });

                    await botClient.EditMessageTextAsync(
                        chatId: chatId,
                        messageId: callbackQuery.Message.MessageId,
                        text: $"Bạn đã chọn {project}. Bạn có muốn xác nhận triển khai không?",
                        replyMarkup: confirmationKeyboard,
                        cancellationToken: cancellationToken);
                    break;

                case string data when data.StartsWith("confirm_yes_"):
                    int index = int.Parse(data.Split('_')[2]);
                    string selectedProject = projects[index];

                    // Xóa tin nhắn chứa các nút Inline trước khi thực hiện triển khai
                    await botClient.DeleteMessageAsync(
                        chatId: chatId,
                        messageId: callbackQuery.Message.MessageId,
                        cancellationToken: cancellationToken);

                    // Sau khi xóa tin nhắn, thực hiện triển khai
                    await DeployCommand.ExecuteAsync(botClient, callbackQuery.Message, selectedProject, cancellationToken);
                    break;

                case "confirm_no":
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
                    break;

                case "start_again":
                    await StartCommand.ExecuteAsync(botClient, callbackQuery.Message, cancellationToken);

                    await botClient.DeleteMessageAsync(
                        chatId: chatId,
                        messageId: callbackQuery.Message.MessageId,
                        cancellationToken: cancellationToken);
                    break;
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        }
    }

    static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
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