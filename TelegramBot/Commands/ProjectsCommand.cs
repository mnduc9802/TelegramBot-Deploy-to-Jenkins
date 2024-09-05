using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using dotenv.net;
using System.Data;
using TelegramBot.DbContext;
using TelegramBot.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Commands
{
    public class ProjectsCommand
    {
        private static readonly string JENKINS_URL;
        private static readonly string DEVOPS_USERNAME;
        private static readonly string DEVOPS_PASSWORD;
        private static readonly string DEVELOPER_USERNAME;
        private static readonly string DEVELOPER_PASSWORD;

        static ProjectsCommand()
        {
            DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));
            JENKINS_URL = Environment.GetEnvironmentVariable("JENKINS_URL");
            DEVOPS_USERNAME = Environment.GetEnvironmentVariable("DEVOPS_USERNAME");
            DEVOPS_PASSWORD = Environment.GetEnvironmentVariable("DEVOPS_PASSWORD");
            DEVELOPER_USERNAME = Environment.GetEnvironmentVariable("DEVELOPER_USERNAME");
            DEVELOPER_PASSWORD = Environment.GetEnvironmentVariable("DEVELOPER_PASSWORD");
        }

        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            try
            {
                var userId = message.From.Id;
                var userRole = await GetUserRoleAsync(userId);
                Console.WriteLine($"User Role: {userRole}");
                Console.WriteLine($"JENKINS_URL: {JENKINS_URL}");
                Console.WriteLine($"DEVELOPER_USERNAME: {DEVELOPER_USERNAME}");
                Console.WriteLine($"DEVELOPER_PASSWORD: {DEVELOPER_PASSWORD}");
                var projects = await GetJenkinsProjectsAsync(userId, userRole);
                var projectsList = "*Danh sách các dự án:*\n" + string.Join("\n", projects.Select((p, i) => $"{i + 1}. {p.Replace("_", "\\_")}"));

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Danh sách các job đã lên lịch", "show_scheduled_jobs")
                    }
                });

                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: projectsList,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Có lỗi xảy ra khi lấy danh sách dự án: {ex.Message}",
                    cancellationToken: cancellationToken);
            }
        }

        public static async Task<string> GetUserRoleAsync(long userId)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT role FROM user_roles WHERE user_id = @userId";
            var parameters = new Dictionary<string, object> { { "@userId", userId } };
            var result = await dbConnection.ExecuteScalarAsync(sql, parameters);
            return result?.ToString() ?? "unknown";
        }

        public static async Task<List<string>> GetJenkinsProjectsAsync(long userId, string userRole)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(JENKINS_URL);
                    string username, password;

                    Console.WriteLine($"Received role: {userRole} for user ID: {userId}");

                    if (string.Equals(userRole, "devops", StringComparison.OrdinalIgnoreCase))
                    {
                        username = DEVOPS_USERNAME;
                        password = DEVOPS_PASSWORD;
                    }
                    else if (string.Equals(userRole, "developer", StringComparison.OrdinalIgnoreCase))
                    {
                        username = DEVELOPER_USERNAME;
                        password = DEVELOPER_PASSWORD;
                    }
                    else
                    {
                        throw new UnauthorizedAccessException($"User with role '{userRole}' does not have permission to access Jenkins projects.");
                    }

                    var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                    var response = await client.GetAsync("/api/json?tree=jobs[name]");
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);

                    return json["jobs"].Select(job => job["name"].ToString()).ToList();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Unauthorized access: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving Jenkins projects: {ex.Message}");
                throw new Exception("Failed to retrieve Jenkins projects", ex);
            }
        }

        private static async Task<List<ScheduledJob>> GetScheduledJobsAsync()
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT job_name, scheduled_time FROM scheduled_jobs ORDER BY scheduled_time";
            var dataTable = await dbConnection.ExecuteReaderAsync(sql);

            var scheduledJobs = new List<ScheduledJob>();
            foreach (DataRow row in dataTable.Rows)
            {
                scheduledJobs.Add(new ScheduledJob
                {
                    JobName = row["job_name"].ToString(),
                    ScheduledTime = Convert.ToDateTime(row["scheduled_time"])
                });
            }

            return scheduledJobs;
        }

        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;

            switch (callbackQuery.Data)
            {
                case "show_scheduled_jobs":
                    await ShowScheduledJobs(botClient, chatId, messageId, cancellationToken);
                    break;
                case string s when s.StartsWith("edit_job_"):
                    await EditJobTime(botClient, chatId, messageId, s.Replace("edit_job_", ""), cancellationToken);
                    break;
                case string s when s.StartsWith("delete_job_"):
                    await DeleteJob(botClient, chatId, messageId, s.Replace("delete_job_", ""), cancellationToken);
                    break;
            }
        }

        private static async Task ShowScheduledJobs(ITelegramBotClient botClient, long chatId, int messageId, CancellationToken cancellationToken)
        {
            var scheduledJobs = await GetScheduledJobsAsync();

            if (!scheduledJobs.Any())
            {
                await botClient.EditMessageTextAsync(
                    chatId: chatId,
                    messageId: messageId,
                    text: "Không có job nào được lên lịch.",
                    cancellationToken: cancellationToken);
                return;
            }

            var jobList = string.Join("\n", scheduledJobs.Select((job, index) =>
                $"{index + 1}. {job.JobName} - {job.ScheduledTime:dd/MM/yyyy HH:mm}"));

            var keyboard = new InlineKeyboardMarkup(
                scheduledJobs.Select(job => new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Sửa {job.JobName}", $"edit_job_{job.JobName}"),
                    InlineKeyboardButton.WithCallbackData($"Xóa {job.JobName}", $"delete_job_{job.JobName}")
                })
            );

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId,
                text: $"Danh sách các job đã lên lịch:\n\n{jobList}",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        private static async Task EditJobTime(ITelegramBotClient botClient, long chatId, int messageId, string jobName, CancellationToken cancellationToken)
        {
            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId,
                text: $"Vui lòng nhập thời gian mới cho job {jobName} (định dạng: DD/MM/YYYY HH:mm)",
                cancellationToken: cancellationToken);

            // Store the job name in the scheduling state to handle the response
            Program.schedulingState[chatId] = $"edit_{jobName}";
        }

        private static async Task DeleteJob(ITelegramBotClient botClient, long chatId, int messageId, string jobName, CancellationToken cancellationToken)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "DELETE FROM scheduled_jobs WHERE job_name = @jobName";
            var parameters = new Dictionary<string, object> { { "@jobName", jobName } };

            await dbConnection.ExecuteNonQueryAsync(sql, parameters);

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId,
                text: $"Đã xóa job {jobName} khỏi danh sách lên lịch.",
                cancellationToken: cancellationToken);

            // Show updated list of scheduled jobs
            await ShowScheduledJobs(botClient, chatId, messageId, cancellationToken);
        }

        public static async Task HandleEditJobTimeInputAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var jobName = Program.schedulingState[chatId].Replace("edit_", "");
            string messageText = message.Text.ToLower().Trim();

            // Kiểm tra nếu tin nhắn chứa "hủy" hoặc "cancel", bao gồm cả trường hợp có mention bot
            if (messageText.Contains("hủy") || messageText.Contains("cancel"))
            {
                // Xóa trạng thái đang chờ nhập thời gian lên lịch
                Program.schedulingState.TryRemove(chatId, out _);
                // Quay lại bước chọn job để deploy
                await botClient.SendTextMessageAsync(
                    chatId,
                    "Lệnh sửa lịch đã bị hủy. Vui lòng /projects để triển khai lại.",
                    cancellationToken: cancellationToken);
                return;
            }

            DateTime scheduledTime;

            // Kiểm tra nếu tin nhắn là "df" để đặt lịch mặc định
            bool isDefaultSchedule = messageText.Contains("df");
            if (isDefaultSchedule)
            {
                scheduledTime = DateTime.Now.AddMinutes(30);
            }
            else if (DateTime.TryParseExact(message.Text, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out scheduledTime))
            {
                if (scheduledTime <= DateTime.Now)
                {
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Thời gian lên lịch phải là trong tương lai. Vui lòng thử lại.",
                        cancellationToken: cancellationToken);
                    return;
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Định dạng thời gian không hợp lệ. Vui lòng nhập lại theo định dạng DD/MM/YYYY HH:mm hoặc nhập 'df' để đặt lịch sau 30 phút.",
                    cancellationToken: cancellationToken);
                return;
            }

            // Cập nhật thời gian lên lịch vào database
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "UPDATE scheduled_jobs SET scheduled_time = @scheduledTime WHERE job_name = @jobName";
            var parameters = new Dictionary<string, object>
            {
                { "@scheduledTime", scheduledTime },
                { "@jobName", jobName }
            };

            await dbConnection.ExecuteNonQueryAsync(sql, parameters);

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Đã cập nhật thời gian cho job {jobName} thành {scheduledTime:dd/MM/yyyy HH:mm}.",
                cancellationToken: cancellationToken);

            // Xóa trạng thái scheduling
            Program.schedulingState.TryRemove(chatId, out _);

            // Chỉ hiển thị danh sách các job đã lên lịch nếu không phải là "df"
            if (!isDefaultSchedule)
            {
                await ShowScheduledJobs(botClient, chatId, message.MessageId, cancellationToken);
            }
        }
    }
}