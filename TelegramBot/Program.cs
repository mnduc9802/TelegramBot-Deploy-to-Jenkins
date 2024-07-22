using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    static ITelegramBotClient botClient;
    static List<string> projects = new List<string> { "Project A", "Project B", "Project C" };
    static string selectedProject = null;

    static async Task Main()
    {
        botClient = new TelegramBotClient("7463243734:AAEXs6bid2YewLvCx6iMxzEqRgW2UweCZX4");

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");

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

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message?.Text != null)
        {
            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text;

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
            }
        }
        else if (update.Type == UpdateType.CallbackQuery)
        {
            var callbackQuery = update.CallbackQuery;
            var chatId = callbackQuery.Message.Chat.Id;

            if (callbackQuery.Data == "deploy")
            {
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
            }
            else if (callbackQuery.Data.StartsWith("deploy_"))
            {
                int projectIndex = int.Parse(callbackQuery.Data.Split('_')[1]);
                selectedProject = projects[projectIndex];

                var confirmationKeyboard = new InlineKeyboardMarkup(new[]
                {
                InlineKeyboardButton.WithCallbackData("Yes", "confirm_yes"),
                InlineKeyboardButton.WithCallbackData("No", "confirm_no")
            });

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Bạn đã chọn {selectedProject}. Bạn có muốn xác nhận triển khai không?",
                    replyMarkup: confirmationKeyboard,
                    cancellationToken: cancellationToken);
            }
            else if (callbackQuery.Data == "confirm_yes" && selectedProject != null)
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Đang triển khai {selectedProject}...",
                    cancellationToken: cancellationToken);

                bool deployResult = await DeployProject(selectedProject);

                if (deployResult)
                {
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: $"Triển khai {selectedProject} thành công!",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: $"Triển khai {selectedProject} thất bại.",
                        cancellationToken: cancellationToken);
                }

                selectedProject = null;
            }
            else if (callbackQuery.Data == "confirm_no")
            {
                var startKeyboard = new InlineKeyboardMarkup(new[]
                {
                InlineKeyboardButton.WithCallbackData("Start", "restart")
            });

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Yêu cầu của bạn đã bị hủy. Nhấn vào nút dưới đây để bắt đầu lại.",
                    replyMarkup: startKeyboard,
                    cancellationToken: cancellationToken);

                selectedProject = null;
            }
            else if (callbackQuery.Data == "help")
            {
                await HelpCommand.ExecuteAsync(botClient, callbackQuery.Message, cancellationToken);
            }
            else if (callbackQuery.Data == "restart")
            {
                var startMessage = new Message
                {
                    Chat = new Chat
                    {
                        Id = chatId
                    }
                };
                await StartCommand.ExecuteAsync(botClient, startMessage, cancellationToken);
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

    static async Task<bool> DeployProject(string project)
    {
        await Task.Delay(5000);
        return true;
    }
}
