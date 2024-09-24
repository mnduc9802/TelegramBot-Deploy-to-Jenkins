using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Services;
using TelegramBot.Utilities.DeployUtilities;

namespace TelegramBot.Commands.MajorCommands.DeployCommand
{
    public class DeployCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, string projectPath, CancellationToken cancellationToken)
        {
            await DeployJob.ExecuteAsync(botClient, message, projectPath, cancellationToken);
        }

        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var userId = callbackQuery.From.Id;
            var userRole = await CredentialService.GetUserRoleAsync(userId);

            if (callbackQuery.Data.StartsWith("deploy_"))
            {
                var shortId = callbackQuery.Data.Replace("deploy_", "");
                var jobUrl = JobKeyboardManager.GetJobUrl(shortId);
                if (jobUrl != null)
                {
                    var jobUrlId = await JobExtension.GetOrCreateJobUrlId(jobUrl, userId);
                    bool hasParameter = jobUrl.EndsWith("_parameter");
                    await DeployConfirmation.JobDeployConfirmationKeyboard(
                        botClient,
                        chatId,
                        jobUrlId.ToString(),
                        hasParameter,
                        cancellationToken,
                        callbackQuery.Message.MessageId
                    );
                }
                else
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Không tìm thấy thông tin job", cancellationToken: cancellationToken);
                }
            }
            else if (callbackQuery.Data.StartsWith("confirm_job_yes_"))
            {
                var jobUrl = callbackQuery.Data.Replace("confirm_job_yes_", "");
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
                var deployResult = await DeployJob.DeployProjectAsync(jobUrl, userRole);
                await SendDeployResultAsync(botClient, chatId, jobUrl, deployResult, cancellationToken);
            }
            else if (callbackQuery.Data == "confirm_job_no")
            {
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
                await botClient.SendTextMessageAsync(chatId, "Yêu cầu triển khai job đã bị hủy.", cancellationToken: cancellationToken);
            }
            else if (callbackQuery.Data.StartsWith("schedule_job_"))
            {
                await ScheduleJob.HandleScheduleJobAsync(botClient, callbackQuery, cancellationToken);
            }
            else
            {
                await JobPaginator.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
        }

        public static string NormalizeJenkinsPath(string projectPath)
        {
            return string.Join("/job/", projectPath.Split(new[] { "job/" }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static async Task SendDeployResultAsync(ITelegramBotClient botClient, long chatId, string project, bool success, CancellationToken cancellationToken)
        {
            var resultMessage = success ? $"Triển khai {project} thành công!" : $"Triển khai {project} thất bại.";
            await botClient.SendTextMessageAsync(chatId, resultMessage, cancellationToken: cancellationToken);
        }
    }
}