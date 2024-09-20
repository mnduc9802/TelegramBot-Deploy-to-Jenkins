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
using TelegramBot.Utilities;

namespace TelegramBot.Commands
{
    public class ProjectsCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            try
            {
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Danh sách các dự án đang triển khai", "show_projects")
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Danh sách các job đã lên lịch", "show_scheduled_jobs")
                    }
                });

                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Chọn một tùy chọn:",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Có lỗi xảy ra: {ex.Message}",
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
                string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(jenkinsUrl);
                    var credentials = EnvironmentVariableLoader.GetCredentialsForRole(userRole);

                    Console.WriteLine($"Received role: {userRole} for user ID: {userId}");

                    var byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}");
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
            var sql = "SELECT job_name, scheduled_time, user_id FROM jobs WHERE scheduled_time IS NOT NULL ORDER BY scheduled_time";
            var dataTable = await dbConnection.ExecuteReaderAsync(sql);
            var scheduledJobs = new List<ScheduledJob>();
            foreach (DataRow row in dataTable.Rows)
            {
                scheduledJobs.Add(new ScheduledJob
                {
                    JobName = row["job_name"].ToString(),
                    ScheduledTime = Convert.ToDateTime(row["scheduled_time"]),
                    UserId = Convert.ToInt64(row["user_id"])
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
                case "show_projects":
                    await ShowProjects(botClient, chatId, callbackQuery.From.Id, cancellationToken);
                    break;
                case "show_scheduled_jobs":
                    await ShowScheduledJobs(botClient, chatId, cancellationToken);
                    break;
                case string s when s.StartsWith("edit_job_"):
                    await EditJobTime(botClient, chatId, messageId, s.Replace("edit_job_", ""), cancellationToken);
                    break;
                case string s when s.StartsWith("delete_job_"):
                    await DeleteJob(botClient, chatId, messageId, s.Replace("delete_job_", ""), cancellationToken);
                    break;
            }
        }

        public static async Task ShowProjects(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
        {
            try
            {
                var userRole = await GetUserRoleAsync(userId);
                var projects = await GetJenkinsProjectsAsync(userId, userRole);
                var projectsList = "*Danh sách các dự án:*\n" + string.Join("\n", projects.Select((p, i) => $"{i + 1}. {p.Replace("_", "\\_")}"));

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: projectsList,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Có lỗi xảy ra khi lấy danh sách dự án: {ex.Message}",
                    cancellationToken: cancellationToken);
            }
        }

        public static async Task ShowScheduledJobs(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var scheduledJobs = await GetScheduledJobsAsync();

            if (!scheduledJobs.Any())
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Không có job nào được lên lịch.",
                    cancellationToken: cancellationToken);
                return;
            }

            var jobList = string.Join("\n", scheduledJobs.Select((job, index) =>
                $"{index + 1}. {job.JobName} - {job.ScheduledTime:dd/MM/yyyy HH:mm}"));

            var jobButtons = scheduledJobs.Select(job => new[]
            {
                InlineKeyboardButton.WithCallbackData($"Sửa {job.JobName}", $"edit_job_{job.JobName}"),
                InlineKeyboardButton.WithCallbackData($"Xóa {job.JobName}", $"delete_job_{job.JobName}")
            }).ToList();


            var navigationButtons = new List<InlineKeyboardButton[]>
            {
                new[] { InlineKeyboardButton.WithCallbackData("📁", "back_to_folder") }
            };

            var keyboard = new InlineKeyboardMarkup(jobButtons.Concat(navigationButtons));

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Danh sách các job đã lên lịch:\n\n{jobList}",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        public static async Task EditJobTime(ITelegramBotClient botClient, long chatId, int messageId, string jobName, CancellationToken cancellationToken)
        {
            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId,
                text: $"Vui lòng nhập thời gian mới cho job {jobName} (định dạng: DD/MM/YYYY HH:mm)",
                cancellationToken: cancellationToken);

            // Store the job name in the scheduling state to handle the response
            Program.schedulingState[chatId] = $"edit_{jobName}";
        }

        public static async Task DeleteJob(ITelegramBotClient botClient, long chatId, int messageId, string jobName, CancellationToken cancellationToken)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "DELETE FROM jobs WHERE job_name = @jobName";
            var parameters = new Dictionary<string, object> { { "@jobName", jobName } };

            await dbConnection.ExecuteNonQueryAsync(sql, parameters);

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId,
                text: $"Đã xóa job {jobName} khỏi danh sách lên lịch.",
                cancellationToken: cancellationToken);

            // Show updated list of scheduled jobs
            await ShowScheduledJobs(botClient, chatId, cancellationToken);
        }

        public static async Task HandleEditJobTimeInputAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var userId = message.From.Id;
            if (!Program.schedulingState.TryGetValue(chatId, out var state) || !state.StartsWith("edit_"))
            {
                await botClient.SendTextMessageAsync(chatId, "Có lỗi xảy ra. Vui lòng thử lại.", cancellationToken: cancellationToken);
                return;
            }

            var jobName = state.Substring(5); // Loại bỏ tiền tố "edit_"
            string messageText = message.Text.Trim();

            // Kiểm tra nếu tin nhắn chứa "hủy" hoặc "cancel"
            if (messageText.ToLower().Contains("hủy") || messageText.ToLower().Contains("cancel"))
            {
                Program.schedulingState.TryRemove(chatId, out _);
                await botClient.SendTextMessageAsync(
                    chatId,
                    "Lệnh sửa lịch đã bị hủy. Vui lòng /projects để xem lại danh sách.",
                    cancellationToken: cancellationToken);
                return;
            }

            DateTime scheduledTime;

            // Kiểm tra nếu tin nhắn là "df" để đặt lịch mặc định
            bool isDefaultSchedule = messageText.ToLower().Contains("df");
            if (isDefaultSchedule)
            {
                scheduledTime = DateTime.Now.AddMinutes(30);
            }
            else
            {
                // Loại bỏ tên bot và các khoảng trắng thừa
                string[] parts = messageText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string timeString = string.Join(" ", parts.Skip(parts[0].StartsWith("@") ? 1 : 0));

                if (DateTime.TryParseExact(timeString, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out scheduledTime))
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
            }

            // Cập nhật thời gian lên lịch vào database
            var dbConnection = new DatabaseConnection(Program.connectionString);
            string sql = "UPDATE jobs SET scheduled_time = @scheduledTime WHERE job_name = @jobName AND user_id = @userId";
            var parameters = new Dictionary<string, object>
            {
                { "@scheduledTime", scheduledTime },
                { "@jobName", jobName },
                { "@userId", userId }
            };

            int rowsAffected = await dbConnection.ExecuteNonQueryAsync(sql, parameters);

            if (rowsAffected > 0)
            {
                string confirmationMessage = $"Đã cập nhật thời gian cho job {jobName} thành {scheduledTime:dd/MM/yyyy HH:mm}.";

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: confirmationMessage,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Không thể cập nhật job {jobName}. Có thể bạn không có quyền chỉnh sửa job này hoặc job không tồn tại.",
                    cancellationToken: cancellationToken);
            }

            // Xóa trạng thái scheduling
            Program.schedulingState.TryRemove(chatId, out _);

            // Hiển thị danh sách các job đã lên lịch
            await ShowScheduledJobs(botClient, chatId, cancellationToken);
        }
    }
}