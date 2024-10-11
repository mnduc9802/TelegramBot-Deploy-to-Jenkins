using System.Data;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Services;

namespace TelegramBot.Commands.Major.DeployCommand
{
    public class ScheduleJob
    {
        public static ConcurrentDictionary<long, string> schedulingState = new ConcurrentDictionary<long, string>();
        public static async Task ExecuteScheduledJob(string jobName, long userId, string parameter)
        {
            try
            {
                var folderPath = Path.GetDirectoryName(jobName)?.Replace("job/", "");
                LoggerService.LogInformation("Executing scheduled deployment. JobName: {JobName}, FolderPath: {FolderPath}, Parameter: {Parameter}, UserId: {UserId}",
                    Path.GetFileName(jobName), folderPath, parameter ?? "none", userId);

                Console.WriteLine($"Attempting to deploy job: {jobName} for user {userId} with parameter: {parameter ?? "None"}");
                var userRole = await CredentialService.GetUserRoleAsync(userId);
                var deployResult = await DeployJob.DeployProjectAsync(jobName, userRole, userId, parameter);
                Console.WriteLine($"Deploy result for {jobName}: {deployResult}");

                if (!deployResult)
                {
                    LoggerService.LogWarning("Scheduled deployment failed. JobName: {JobName}, FolderPath: {FolderPath}, UserId: {UserId}",
                        Path.GetFileName(jobName), folderPath, userId);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, "Error executing scheduled job. JobName: {JobName}, UserId: {UserId}", jobName, userId);
                Console.WriteLine($"Error executing scheduled job {jobName}: {ex.Message}");
            }
        }

        public static async Task HandleScheduleJobAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var jobUrlId = callbackQuery.Data.Replace("schedule_job_", "");
            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
            await RequestScheduleTimeAsync(botClient, callbackQuery.Message.Chat.Id, jobUrlId, cancellationToken);
        }

        public static async Task HandleScheduleTimeInputAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            string messageText = message.Text.Trim();

            if (messageText.ToLower().Contains("hủy") || messageText.ToLower().Contains("cancel"))
            {
                schedulingState.TryRemove(message.Chat.Id, out _);
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Lệnh lên lịch đã bị hủy. Vui lòng /deploy để triển khai lại.",
                    cancellationToken: cancellationToken);
                return;
            }

            DateTime scheduledTime;

            if (messageText.ToLower().Contains("df"))
            {
                scheduledTime = DateTime.Now.AddMinutes(30);
            }
            else if (DateTime.TryParseExact(messageText, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out scheduledTime))
            {
                if (scheduledTime <= DateTime.Now)
                {
                    await botClient.SendTextMessageAsync(
                        message.Chat.Id,
                        "Thời gian lên lịch phải là trong tương lai. Vui lòng thử lại.",
                        cancellationToken: cancellationToken);
                    return;
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Định dạng thời gian không hợp lệ. Vui lòng nhập lại theo định dạng DD/MM/YYYY HH:mm hoặc nhập 'df' để đặt lịch sau 30 phút.",
                    cancellationToken: cancellationToken);
                return;
            }

            string[] parts = schedulingState[message.Chat.Id].Split('_');
            const int JOB_URL_ID_INDEX = 2;
            string jobUrlId = parts[JOB_URL_ID_INDEX];

            var jobUrl = await JobService.GetJobUrlFromId(int.Parse(jobUrlId));
            bool hasParameter = jobUrl.EndsWith("_parameter");

            LoggerService.LogInformation("Schedule time set for job. JobUrl: {JobUrl}, ScheduledTime: {ScheduledTime}, HasParameter: {HasParameter}, UserId: {UserId}",
                jobUrl, scheduledTime, hasParameter, message.From.Id);

            if (hasParameter)
            {
                await RequestScheduleVersionAsync(botClient, message.Chat.Id, jobUrlId, scheduledTime, cancellationToken);
            }
            else
            {
                await JobService.HandleScheduleInputAsync(botClient, message, jobUrl, scheduledTime, null, cancellationToken);
            }
        }

        public static async Task HandleScheduleParameterInputAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            const int JOB_DETAILS_INDEX = 0; // Chỉ số của chuỗi chứa thông tin chi tiết về job
            const int SCHEDULE_TIME_INDEX = 1; // Chỉ số của thời gian lên lịch
            const int JOB_URL_ID_INDEX = 2; // Chỉ số của jobUrlId trong chuỗi jobParts

            string parameter = message.Text.Trim();
            string[] jobInfo = schedulingState[message.Chat.Id].Split('|');
            string[] jobParts = jobInfo[JOB_DETAILS_INDEX].Split('_');
            string jobUrlId = jobParts[JOB_URL_ID_INDEX];
            DateTime scheduledTime = DateTime.Parse(jobInfo[SCHEDULE_TIME_INDEX]);

            string jobUrl = await JobService.GetJobUrlFromId(int.Parse(jobUrlId));

            LoggerService.LogInformation("Parameter set for scheduled job. JobUrl: {JobUrl}, Parameter: {Parameter}, ScheduledTime: {ScheduledTime}, UserId: {UserId}",
                jobUrl, parameter, scheduledTime, message.From.Id);

            await JobService.HandleScheduleInputAsync(botClient, message, jobUrl, scheduledTime, parameter, cancellationToken);
        }

        public static async Task RequestScheduleTimeAsync(ITelegramBotClient botClient, long chatId, string jobUrlId, CancellationToken cancellationToken)
        {
            var scheduledJobs = await JobService.GetScheduledJobsAsync();

            string jobList = scheduledJobs.Any()
                ? string.Join("\n", scheduledJobs.Select((j, index) => $"{index + 1}. {j.JobName} - {j.ScheduledTime:dd/MM/yyyy HH:mm}"))
                : "Chưa có job nào được lên lịch.";

            var message = $"*Các job đang được lên lịch:*\n\n{jobList}\n\nVui lòng nhập thời gian bạn muốn lên lịch triển khai job (định dạng: DD/MM/YYYY HH:mm)";

            await botClient.SendTextMessageAsync(
                chatId,
                message,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: cancellationToken);

            schedulingState[chatId] = $"schedule_time_{jobUrlId}";
        }

        public static async Task RequestScheduleVersionAsync(ITelegramBotClient botClient, long chatId, string jobUrlId, DateTime scheduledTime, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "Vui lòng nhập tham số VERSION cho job được lên lịch:",
                cancellationToken: cancellationToken);

            schedulingState[chatId] = $"schedule_version_{jobUrlId}|{scheduledTime:yyyy-MM-dd HH:mm:ss}";
        }
    }
}