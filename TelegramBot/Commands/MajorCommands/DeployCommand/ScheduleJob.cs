using Telegram.Bot.Types;
using Telegram.Bot;
using System.Data;
using TelegramBot.Services;

namespace TelegramBot.Commands.MajorCommands.DeployCommand
{
    public class ScheduleJob
    {
        public static async Task ExecuteScheduledJob(string jobName, long userId, string parameter)
        {
            try
            {
                Console.WriteLine($"Attempting to deploy job: {jobName} for user {userId} with parameter: {parameter ?? "None"}");
                var userRole = await CredentialService.GetUserRoleAsync(userId);
                var deployResult = await DeployJob.DeployProjectAsync(jobName, userRole, parameter);
                Console.WriteLine($"Deploy result for {jobName}: {deployResult}");
            }
            catch (Exception ex)
            {
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
                Program.schedulingState.TryRemove(message.Chat.Id, out _);
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

            string[] parts = Program.schedulingState[message.Chat.Id].Split('_');
            const int JOB_URL_ID_INDEX = 2; // Vị trí của jobUrlId trong mảng parts
            string jobUrlId = parts[JOB_URL_ID_INDEX];

            var jobUrl = await JobService.GetJobUrlFromId(int.Parse(jobUrlId));
            bool hasParameter = jobUrl.EndsWith("_parameter");

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
            string[] jobInfo = Program.schedulingState[message.Chat.Id].Split('|');
            string[] jobParts = jobInfo[JOB_DETAILS_INDEX].Split('_');
            string jobUrlId = jobParts[JOB_URL_ID_INDEX];
            DateTime scheduledTime = DateTime.Parse(jobInfo[SCHEDULE_TIME_INDEX]);

            // Fetch the correct job URL using the jobUrlId
            string jobUrl = await JobService.GetJobUrlFromId(int.Parse(jobUrlId));

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

            Program.schedulingState[chatId] = $"schedule_time_{jobUrlId}";
        }

        public static async Task RequestScheduleVersionAsync(ITelegramBotClient botClient, long chatId, string jobUrlId, DateTime scheduledTime, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "Vui lòng nhập tham số VERSION cho job được lên lịch:",
                cancellationToken: cancellationToken);

            Program.schedulingState[chatId] = $"schedule_version_{jobUrlId}|{scheduledTime:yyyy-MM-dd HH:mm:ss}";
        }
    }
}