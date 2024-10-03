using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Commands.MajorCommands.ProjectCommand;
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
            if (callbackQuery.Message == null)
            {
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Invalid callback query", cancellationToken: cancellationToken);
                return;
            }

            var chatId = callbackQuery.Message.Chat.Id;
            const long UNKNOWN_USER_ID = 0;
            var userId = callbackQuery.From?.Id ?? UNKNOWN_USER_ID;
            var userRole = await CredentialService.GetUserRoleAsync(userId);

            if (callbackQuery.Data?.StartsWith("deploy_") == true)
            {
                var shortId = callbackQuery.Data.Replace("deploy_", "");
                var jobUrl = JobKeyboardManager.GetJobUrl(shortId);
                if (jobUrl != null)
                {
                    var jobUrlId = await JobService.GetOrCreateJobUrlId(jobUrl, userId);
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
            else if (callbackQuery.Data?.StartsWith("confirm_job_yes_") == true)
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
            else if (callbackQuery.Data?.StartsWith("schedule_job_") == true)
            {
                await ScheduleJob.HandleScheduleJobAsync(botClient, callbackQuery, cancellationToken);
            }
            else
            {
                await JobPaginator.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
        }


        public static async Task HandleDeployCallback(ITelegramBotClient botClient,CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            const int DEPLOY_PREFIX_LENGTH = 7; // Chiều dài tiền tố "deploy_"
            var data = callbackQuery.Data?.Substring(DEPLOY_PREFIX_LENGTH);
            Console.WriteLine($"Callback data: {data}");

            if (int.TryParse(data, out int projectIndex))
            {
                Console.WriteLine($"Project Index: {projectIndex}");
                await DeployConfirmation.DeployConfirmationKeyboard(botClient, callbackQuery, projectIndex, cancellationToken);
            }
            else
            {
                Console.WriteLine("Delegating to DeployCommand");
                await HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
        }

        public static async Task ShowProjectsKeyboard(ITelegramBotClient botClient,long chatId, long userId, CancellationToken cancellationToken)
        {
            var userRole = await CredentialService.GetUserRoleAsync(userId);
            Console.WriteLine($"Fetching Jenkins projects for userId: {userId} with role: {userRole}");

            var projects = await JenkinsProject.GetJenkinsProjectsAsync(userId, userRole);

            if (projects == null || !projects.Any())
            {
                Console.WriteLine("No projects found for the user.");
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Không tìm thấy dự án nào.",
                    cancellationToken: cancellationToken);
                return;
            }

            FolderPaginator.chatState[chatId] = projects;
            await FolderPaginator.ShowFoldersPage(botClient, chatId, projects, 0, cancellationToken);
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