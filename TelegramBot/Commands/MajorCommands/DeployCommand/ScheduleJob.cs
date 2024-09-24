using Telegram.Bot.Types;
using Telegram.Bot;
using TelegramBot.DbContext;
using System.Data;
using TelegramBot.Models;
using System.Timers;
using TelegramBot.Services;

namespace TelegramBot.Commands.MajorCommands.DeployCommand
{
    public class ScheduleJob
    {
        private static System.Timers.Timer timer;

        public static void Initialize()
        {
            timer = new System.Timers.Timer(60000);
            timer.Elapsed += CheckScheduledJobs;
            timer.Start();
        }

        private static async void CheckScheduledJobs(object sender, ElapsedEventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                var dbConnection = new DatabaseConnection(Program.connectionString);
                var sql = "SELECT job_name, scheduled_time, user_id, parameter FROM jobs WHERE scheduled_time <= @now";
                var parameters = new Dictionary<string, object> { { "@now", now } };
                var dataTable = await dbConnection.ExecuteReaderAsync(sql, parameters);

                foreach (DataRow row in dataTable.Rows)
                {
                    var jobName = row["job_name"].ToString();
                    var scheduledTime = Convert.ToDateTime(row["scheduled_time"]);
                    var userId = Convert.ToInt64(row["user_id"]);
                    var parameter = row["parameter"] as string;
                    Console.WriteLine($"Executing scheduled job: {jobName} at {scheduledTime} for user {userId} with parameter: {parameter ?? "None"}");

                    await ExecuteScheduledJob(jobName, userId, parameter);

                    var deleteSql = "DELETE FROM jobs WHERE job_name = @jobName AND scheduled_time = @scheduledTime";
                    var deleteParameters = new Dictionary<string, object>
                        {
                            { "@jobName", jobName },
                            { "@scheduledTime", scheduledTime }
                        };
                    await dbConnection.ExecuteNonQueryAsync(deleteSql, deleteParameters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CheckScheduledJobs: {ex.Message}");
            }
        }

        private static async Task ExecuteScheduledJob(string jobName, long userId, string parameter)
        {
            try
            {
                Console.WriteLine($"Attempting to deploy job: {jobName} for user {userId} with parameter: {parameter ?? "None"}");
                var userRole = await CredentialService.GetUserRoleAsync(userId);
                var deployResult = await DeployJob.DeployProjectAsync(jobName, userRole, parameter);
                Console.WriteLine($"Deploy result for {jobName}: {deployResult}");

                // TODO: Notify the user about the deployment result
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing scheduled job {jobName}: {ex.Message}");
            }
        }
    
        public static async Task<List<Job>> GetScheduledJobsAsync()
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT job_name, scheduled_time, created_at FROM jobs WHERE scheduled_time IS NOT NULL ORDER BY scheduled_time";
            var dataTable = await dbConnection.ExecuteReaderAsync(sql);

            var scheduledJobs = new List<Job>();
            foreach (DataRow row in dataTable.Rows)
            {
                scheduledJobs.Add(new Job
                {
                    JobName = row["job_name"].ToString(),
                    ScheduledTime = Convert.ToDateTime(row["scheduled_time"]),
                    CreatedAt = Convert.ToDateTime(row["created_at"])
                });
            }

            return scheduledJobs;
        }
        public static async Task HandleScheduleJobAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var jobUrlId = callbackQuery.Data.Replace("schedule_job_", "");
            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);

            await RequestScheduleTimeAsync(botClient, callbackQuery.Message.Chat.Id, jobUrlId, cancellationToken);
        }

        public static async Task HandleScheduleInputAsync(ITelegramBotClient botClient, Message message, string jobUrl, DateTime scheduledTime, string parameter, CancellationToken cancellationToken)
        {
            string jobName = jobUrl.TrimEnd('/');
            var dbConnection = new DatabaseConnection(Program.connectionString);
            string sql;
            Dictionary<string, object> parameters;

            // Check if the job already exists
            sql = "SELECT id FROM jobs WHERE url = @url AND user_id = @userId";
            parameters = new Dictionary<string, object>
            {
                { "@url", jobUrl },
                { "@userId", message.From.Id }
            };
            var existingJobId = await dbConnection.ExecuteScalarAsync(sql, parameters);

            if (existingJobId != null)
            {
                // Update existing job
                sql = "UPDATE jobs SET scheduled_time = @scheduledTime, parameter = @parameter WHERE id = @jobId";
                parameters = new Dictionary<string, object>
                {
                    { "@scheduledTime", scheduledTime },
                    { "@parameter", parameter ?? (object)DBNull.Value },
                    { "@jobId", existingJobId }
                };
            }
            else
            {
                // Insert new job
                sql = "INSERT INTO jobs (job_name, url, scheduled_time, created_at, user_id, parameter) VALUES (@jobName, @url, @scheduledTime, @createdAt, @userId, @parameter)";
                parameters = new Dictionary<string, object>
                {
                    { "@jobName", jobName },
                    { "@url", jobUrl },
                    { "@scheduledTime", scheduledTime },
                    { "@createdAt", DateTime.Now },
                    { "@userId", message.From.Id },
                    { "@parameter", parameter ?? (object)DBNull.Value }
                };
            }

            await dbConnection.ExecuteNonQueryAsync(sql, parameters);

            string confirmationMessage = parameter != null
                ? $"Đã lên lịch triển khai job {jobName} vào lúc {scheduledTime:dd/MM/yyyy HH:mm} với tham số VERSION: {parameter}."
                : $"Đã lên lịch triển khai job {jobName} vào lúc {scheduledTime:dd/MM/yyyy HH:mm}.";

            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                confirmationMessage,
                cancellationToken: cancellationToken);

            Program.schedulingState.TryRemove(message.Chat.Id, out _);
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
            string jobUrlId = parts[2];

            var jobUrl = await JobExtension.GetJobUrlFromId(int.Parse(jobUrlId));
            bool hasParameter = jobUrl.EndsWith("_parameter");

            if (hasParameter)
            {
                await RequestScheduleVersionAsync(botClient, message.Chat.Id, jobUrlId, scheduledTime, cancellationToken);
            }
            else
            {
                await HandleScheduleInputAsync(botClient, message, jobUrl, scheduledTime, null, cancellationToken);
            }
        }

        public static async Task HandleScheduleParameterInputAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            string parameter = message.Text.Trim();
            string[] jobInfo = Program.schedulingState[message.Chat.Id].Split('|');
            string[] jobParts = jobInfo[0].Split('_');
            string jobUrlId = jobParts[2]; // This should be the correct job ID
            DateTime scheduledTime = DateTime.Parse(jobInfo[1]);

            // Fetch the correct job URL using the jobUrlId
            string jobUrl = await JobExtension.GetJobUrlFromId(int.Parse(jobUrlId));

            await HandleScheduleInputAsync(botClient, message, jobUrl, scheduledTime, parameter, cancellationToken);
        }

        public static async Task RequestScheduleTimeAsync(ITelegramBotClient botClient, long chatId, string jobUrlId, CancellationToken cancellationToken)
        {
            var scheduledJobs = await GetScheduledJobsAsync();

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