using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Commands.Major.Deploy;
using TelegramBot.Commands.Major.Project;
using TelegramBot.Commands.Minor;
using TelegramBot.Services;
using TelegramBot.Utilities.Deploy;
using TelegramBot.Utilities.Deploy.FolderUtilities;
using TelegramBot.Utilities.Deploy.JobUtilities;

namespace TelegramBot.Core.Handlers
{
    public static class MessageHandler
    {
        public static async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            try
            {
                LogMessageInfo(message);

                var chatId = message.Chat.Id;
                var text = message.Text;

                if (string.IsNullOrEmpty(text))
                    return;

                await ProcessMessageText(botClient, message, text, chatId, cancellationToken);
            }
            catch (Exception ex)
            {
                await HandleMessageError(botClient, ex, message, cancellationToken);
            }
        }

        public static void LogMessageInfo(Message message)
        {
            LoggerService.LogInformation(
            "Handling message. MessageId: {MessageId}, ChatId: {ChatId}, Username: {Username}, Text: {Text}",
            message.MessageId, message.Chat.Id, message.From?.Username ?? "Unknown", message.Text ?? "No text");
        }

        public static async Task ProcessMessageText(ITelegramBotClient botClient, Message message, string text, long chatId, CancellationToken cancellationToken)
        {
            var botUsername = (await botClient.GetMeAsync(cancellationToken)).Username;
            text = NormalizeBotCommand(text, botUsername);

            if (message.Chat.Type != ChatType.Private)
            {
                // Trong nhóm, chỉ xử lý nếu có @botUsername hoặc không có @ nào
                if (!text.Contains("@") || text.EndsWith("@" + botUsername))
                {
                    text = NormalizeBotCommand(text, botUsername);
                }
                else
                {
                    return; // Bỏ qua nếu có @ nhưng không phải cho bot này
                }
            }
            else
            {
                // Trong chat riêng, xử lý tất cả các lệnh
                text = NormalizeBotCommand(text, botUsername);
            }

            await HandleSpecialStates(botClient, message, chatId, cancellationToken);
            await HandleBotCommands(botClient, message, text, cancellationToken);
        }

        public static string NormalizeBotCommand(string text, string botUsername)
        {
            var commandParts = text.Split('@');
            if (commandParts.Length >= 2)
            {
                // Nếu có @, chỉ lấy phần lệnh
                return commandParts[0];
            }
            return text;
        }

        public static async Task HandleSpecialStates(ITelegramBotClient botClient, Message message, long chatId, CancellationToken cancellationToken)
        {
            await CombinedSearchUtility.HandleSearchQuery(botClient, message, cancellationToken);
            

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

        public static async Task HandleScheduleStates(ITelegramBotClient botClient, Message message, string state, CancellationToken cancellationToken)
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

        public static async Task HandleBotCommands(ITelegramBotClient botClient, Message message, string text, CancellationToken cancellationToken)
        {
            if (text.StartsWith("/"))
            {
                var command = text.Split(' ')[0].ToLower();
                switch (command)
                {
                    case "/start":
                        await StartCommand.ExecuteAsync(botClient, message, cancellationToken);
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

        public static async Task HandleMessageError(ITelegramBotClient botClient, Exception ex, Message message, CancellationToken cancellationToken)
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
}