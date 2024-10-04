using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Commands.MajorCommands.DeployCommand;
using TelegramBot.Commands.MajorCommands.ProjectCommand;
using TelegramBot.Services;

namespace TelegramBot.Utilities.DeployUtilities
{
    public static class DeployConfirmation
    {
        public static async Task DeployConfirmationKeyboard(ITelegramBotClient botClient, CallbackQuery callbackQuery, int projectIndex, CancellationToken cancellationToken)
        {
            var userId = callbackQuery.From.Id;
            var userRole = await CredentialService.GetUserRoleAsync(userId);
            var projects = await JenkinsProject.GetJenkinsProjectsAsync(userId, userRole);

            // Kiểm tra nếu danh sách projects không rỗng và projectIndex hợp lệ
            if (projects == null || !projects.Any() || projectIndex < 0 || projectIndex >= projects.Count)
            {
                await botClient.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: "Dự án không hợp lệ hoặc đã bị thay đổi. Vui lòng thử lại.",
                    cancellationToken: cancellationToken);

                return;
            }
        }

        public static async Task HandleConfirmYesCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var projectIndex = int.Parse(callbackQuery.Data.Split('_')[2]);
            var userId = callbackQuery.From.Id;
            var userRole = await CredentialService.GetUserRoleAsync(userId);
            var projects = await JenkinsProject.GetJenkinsProjectsAsync(userId, userRole);

            if (projects == null || !projects.Any() || projectIndex < 0 || projectIndex >= projects.Count)
            {
                await botClient.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: "Dự án không hợp lệ hoặc đã bị thay đổi. Vui lòng thử lại.",
                    cancellationToken: cancellationToken);

                return;
            }

            var selectedProject = projects[projectIndex];

            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
            await DeployCommand.ExecuteAsync(botClient, callbackQuery.Message, selectedProject, cancellationToken);
        }

        public static async Task HandleConfirmNoCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(
                chatId: callbackQuery.Message.Chat.Id,
                text: "Yêu cầu /deploy của bạn đã bị hủy.",
                cancellationToken: cancellationToken);

            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
        }

        public static async Task JobDeployConfirmationKeyboard(ITelegramBotClient botClient, long chatId, string jobUrlId, bool hasParameter, CancellationToken cancellationToken, int? messageId = null)
        {
            var buttons = new List<InlineKeyboardButton[]>();

            if (hasParameter)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("Không", "confirm_job_no"),
                    InlineKeyboardButton.WithCallbackData("Đồng ý", $"enter_version_{jobUrlId}")
                });
            }
            else
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("Không", "confirm_job_no"),
                    InlineKeyboardButton.WithCallbackData("Đồng ý", $"confirm_job_yes_{jobUrlId}")
                });
            }

            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("Lên lịch", $"schedule_job_{jobUrlId}")
            });

            var confirmationKeyboard = new InlineKeyboardMarkup(buttons);

            string message = $"Bạn đã chọn job với ID {jobUrlId}. Bạn muốn thực hiện hành động nào?";

            if (messageId.HasValue)
            {
                await botClient.EditMessageTextAsync(
                    chatId: chatId,
                    messageId: messageId.Value,
                    text: message,
                    replyMarkup: confirmationKeyboard,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: message,
                    replyMarkup: confirmationKeyboard,
                    cancellationToken: cancellationToken);
            }
        }

        public static async Task HandleConfirmJobYesCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            const int JOB_URL_ID_INDEX = 3; // Define the index for jobUrlId as a constant
            var jobUrlId = int.Parse(callbackQuery.Data.Split('_')[JOB_URL_ID_INDEX]);
            var userId = callbackQuery.From.Id;
            var userRole = await CredentialService.GetUserRoleAsync(userId);

            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
            var jobUrl = await JobService.GetJobUrlFromId(jobUrlId);
            if (jobUrl != null)
            {
                var deployResult = await DeployJob.DeployProjectAsync(jobUrl, userRole);
                await DeployCommand.SendDeployResultAsync(botClient, callbackQuery.Message.Chat.Id, jobUrl, deployResult, cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Không tìm thấy thông tin job", cancellationToken: cancellationToken);
            }
        }

        public static async Task HandleConfirmJobNoCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(
                chatId: callbackQuery.Message.Chat.Id,
                text: "Yêu cầu triển khai job đã bị hủy.",
                cancellationToken: cancellationToken);

            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
        }
    }
}