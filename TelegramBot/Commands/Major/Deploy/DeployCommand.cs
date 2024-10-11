using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Commands.Major.ProjectCommand;
using TelegramBot.Services;
using TelegramBot.Utilities.DeployUtilities;

namespace TelegramBot.Commands.Major.DeployCommand
{
    public class DeployCommand
    {
        public static ConcurrentDictionary<long, string> versionInputState = new ConcurrentDictionary<long, string>();

        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, string projectPath, CancellationToken cancellationToken)
        {
            if (message.From == null)
            {
                throw new ArgumentException("Message.From cannot be null", nameof(message));
            }

            var userId = message.From.Id;
            LoggerService.LogInformation("Starting deploy command execution. UserId: {UserId}, ProjectPath: {ProjectPath}", userId, projectPath);

            try
            {
                await DeployJob.ExecuteAsync(botClient, message, projectPath, userId, cancellationToken);
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, "Error executing deploy command. UserId: {UserId}", userId);
                throw;
            }
        }

        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            if (callbackQuery.Message == null)
            {
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Invalid callback query", cancellationToken: cancellationToken);
                return;
            }

            var chatId = callbackQuery.Message.Chat.Id;
            // Always use the original caller's userId
            var userId = callbackQuery.From?.Id ?? throw new ArgumentNullException(nameof(callbackQuery.From.Id));
            Console.WriteLine($"HandleCallbackQueryAsync - Using userId: {userId}");
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
                var deployResult = await DeployJob.DeployProjectAsync(jobUrl, userRole, userId);
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

        public static async Task HandleDeployCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            const int DEPLOY_PREFIX_LENGTH = 7;
            var data = callbackQuery.Data?.Substring(DEPLOY_PREFIX_LENGTH);
            var userId = callbackQuery.From?.Id ?? throw new ArgumentNullException(nameof(callbackQuery.From.Id));
            Console.WriteLine($"HandleDeployCallback - Using userId: {userId} for data: {data}");

            if (int.TryParse(data, out int projectIndex))
            {
                Console.WriteLine($"Project Index: {projectIndex}, UserId: {userId}");
                await DeployConfirmation.DeployConfirmationKeyboard(botClient, callbackQuery, projectIndex, cancellationToken);
            }
            else
            {
                Console.WriteLine($"Delegating to HandleCallbackQueryAsync, UserId: {userId}");
                await HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
        }

        public static async Task HandleVersionInputAsync(ITelegramBotClient botClient, Message message, string jobUrlId, CancellationToken cancellationToken)
        {
            var userId = message.From?.Id ?? throw new ArgumentNullException(nameof(message.From.Id));
            var version = message.Text.Trim();
            versionInputState.TryRemove(message.Chat.Id, out _);

            var userRole = await CredentialService.GetUserRoleAsync(userId);
            var jobUrl = await JobService.GetJobUrlFromId(int.Parse(jobUrlId));
            if (!string.IsNullOrEmpty(jobUrl))
            {
                // Thêm log cho folder path và job name
                var folderPath = Path.GetDirectoryName(jobUrl)?.Replace("job/", "");
                var jobName = Path.GetFileName(jobUrl);
                LoggerService.LogInformation("Processing version input for deployment. JobName: {JobName}, FolderPath: {FolderPath}, Version: {Version}, UserId: {UserId}", jobName, folderPath, version, userId);

                await JobService.GetOrCreateJobUrlId(jobUrl, userId, version);
                var deployResult = await DeployJob.DeployProjectAsync(jobUrl, userRole, userId, version);
                await SendDeployResultAsync(botClient, message.Chat.Id, jobUrl, deployResult, cancellationToken);
            }
            else
            {
                LoggerService.LogWarning("Job URL not found for deployment. JobUrlId: {JobUrlId}, UserId: {UserId}", jobUrlId, userId);
                await botClient.SendTextMessageAsync(message.Chat.Id, "Không tìm thấy thông tin job", cancellationToken: cancellationToken);
            }
        }

        public static async Task ShowProjectsKeyboard(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
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

        public static async Task SendDeployResultAsync(ITelegramBotClient botClient, long chatId, string project, bool success, CancellationToken cancellationToken)
        {
            var folderPath = Path.GetDirectoryName(project)?.Replace("job/", "");
            var jobName = Path.GetFileName(project);

            // Get user information
            var user = await botClient.GetChatAsync(chatId, cancellationToken);
            string userIdentifier = UserService.GetUserIdentifier(user);

            var resultMessage = success
                ? $"{userIdentifier} Triển khai {project} thành công!"
                : $"{userIdentifier} Triển khai {project} thất bại.";

            LoggerService.LogInformation(
                "Deploy result. User: {User}, JobName: {JobName}, FolderPath: {FolderPath}, Success: {Success}, ChatId: {ChatId}",
                userIdentifier, jobName, folderPath, success, chatId);

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: resultMessage,
                cancellationToken: cancellationToken);
        }
    }
}