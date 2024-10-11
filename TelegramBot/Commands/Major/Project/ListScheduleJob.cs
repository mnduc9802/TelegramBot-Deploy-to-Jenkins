using System.Data;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Data.DbContext;
using TelegramBot.Data.Models;
using TelegramBot.Commands.Major.DeployCommand;

namespace TelegramBot.Commands.Major.ProjectCommand
{
    public class ListScheduleJob
    {
        public static async Task<List<Job>> GetScheduledJobsAsync()
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT job_name, scheduled_time, user_id FROM jobs WHERE scheduled_time IS NOT NULL ORDER BY scheduled_time";
            var dataTable = await dbConnection.ExecuteReaderAsync(sql);
            var scheduledJobs = new List<Job>();
            foreach (DataRow row in dataTable.Rows)
            {
                scheduledJobs.Add(new Job
                {
                    JobName = row["job_name"].ToString(),
                    ScheduledTime = Convert.ToDateTime(row["scheduled_time"]),
                    UserId = Convert.ToInt64(row["user_id"])
                });
            }
            return scheduledJobs;
        }
        public static async Task EditJobTime(ITelegramBotClient botClient, long chatId, int messageId, string jobName, CancellationToken cancellationToken)
        {
            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId,
                text: $"Vui lòng nhập thời gian mới cho job {jobName} (định dạng: DD/MM/YYYY HH:mm)",
                cancellationToken: cancellationToken);

            // Store the job name in the scheduling state to handle the response
            ScheduleJob.schedulingState[chatId] = $"edit_{jobName}";
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
            await ProjectCommand.ShowScheduledJobs(botClient, chatId, cancellationToken);
        }

        public static async Task HandleEditJobTimeInputAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var userId = message.From?.Id;
            if (!ScheduleJob.schedulingState.TryGetValue(chatId, out var state) || !state.StartsWith("edit_"))
            {
                await botClient.SendTextMessageAsync(chatId, "Có lỗi xảy ra. Vui lòng thử lại.", cancellationToken: cancellationToken);
                return;
            }

            const int EDIT_PREFIX_LENGTH = 5;
            var jobName = state.Substring(EDIT_PREFIX_LENGTH); // Loại bỏ tiền tố "edit_"
            string messageText = message.Text.Trim();

            // Kiểm tra nếu tin nhắn chứa "hủy" hoặc "cancel"
            if (messageText.ToLower().Contains("hủy") || messageText.ToLower().Contains("cancel"))
            {
                ScheduleJob.schedulingState.TryRemove(chatId, out _);
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
            ScheduleJob.schedulingState.TryRemove(chatId, out _);

            // Hiển thị danh sách các job đã lên lịch
            await ProjectCommand.ShowScheduledJobs(botClient, chatId, cancellationToken);
        }
    }
}