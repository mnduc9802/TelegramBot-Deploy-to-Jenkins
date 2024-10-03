using System.Data;
using System.Timers;
using Telegram.Bot.Types;
using Telegram.Bot;
using TelegramBot.DbContext;
using TelegramBot.Models;
using TelegramBot.Utilities.EnvironmentUtilities;
using TelegramBot.Commands.MajorCommands.DeployCommand;

namespace TelegramBot.Services
{
    public class JobService
    {
        #region General
        public static async Task ResetJobsTableIfNeeded()
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);

            try
            {
                // Check if the maximum ID has reached 9999
                var checkSql = "SELECT MAX(id) FROM jobs";
                var maxId = await dbConnection.ExecuteScalarAsync(checkSql);

                Console.WriteLine($"Current ID: {maxId}"); // Logging for debugging

                const int MAX_JOB_ID = 9999;
                if (maxId != null && Convert.ToInt32(maxId) >= MAX_JOB_ID)
                {
                    // Perform the reset
                    var resetSql = @"TRUNCATE TABLE jobs RESTART IDENTITY;";

                    await dbConnection.ExecuteNonQueryAsync(resetSql);

                    Console.WriteLine("Jobs table has been reset. ID sequence restarted from 1.");

                    // Verify the reset
                    var verifyMaxId = await dbConnection.ExecuteScalarAsync(checkSql);
                    Console.WriteLine($"New max ID after reset: {verifyMaxId}"); // Logging for debugging
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ResetJobsTableIfNeeded: {ex.Message}");
            }
        }
        #endregion

        #region NormalJob
        public static async Task<int> GetOrCreateJobUrlId(string url, long userId, string parameter = null)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
            const string JOB_PREFIX = "job/";

            // Process the URL more safely
            string fullPath = url;
            string jobName = url;
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                fullPath = uri.AbsolutePath.TrimStart('/');
                jobName = uri.Segments.Last().TrimEnd('/');
            }
            else if (url.Contains("/"))
            {
                jobName = url.Split('/').Last().TrimEnd('/');
            }

            if (fullPath.StartsWith(JOB_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                // Sử dụng hằng số JOB_PREFIX để loại bỏ tiền tố "job/"
                fullPath = fullPath.Substring(JOB_PREFIX.Length);
            }

            var selectSql = "SELECT id FROM jobs WHERE url = @url";
            var selectParams = new Dictionary<string, object> { { "@url", fullPath } };
            var result = await dbConnection.ExecuteScalarAsync(selectSql, selectParams);

            if (result != null)
            {
                int jobId = Convert.ToInt32(result);
                // Job exists, update the created_at timestamp and parameter
                var updateSql = "UPDATE jobs SET created_at = @created_at, parameter = @parameter WHERE id = @id";
                var updateParams = new Dictionary<string, object>
                {
                    { "@created_at", DateTime.Now },
                    { "@id", jobId },
                    { "@parameter", parameter ?? (object)DBNull.Value }
                };
                await dbConnection.ExecuteNonQueryAsync(updateSql, updateParams);
                await ResetJobsTableIfNeeded();
                return jobId;
            }

            // Job doesn't exist, create a new one
            var insertSql = "INSERT INTO jobs (job_name, url, user_id, created_at, parameter) VALUES (@job_name, @url, @user_id, @created_at, @parameter) RETURNING id";
            var insertParams = new Dictionary<string, object>
            {
                { "@job_name", jobName },
                { "@url", fullPath },
                { "@user_id", userId },
                { "@created_at", DateTime.Now },
                { "@parameter", parameter ?? (object)DBNull.Value }
            };
            result = await dbConnection.ExecuteScalarAsync(insertSql, insertParams);
            return Convert.ToInt32(result);
        }

        public static async Task<string> GetJobUrlFromId(int id)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT url FROM jobs WHERE id = @id";
            var parameters = new Dictionary<string, object> { { "@id", id } };
            var result = await dbConnection.ExecuteScalarAsync(sql, parameters);

            return result?.ToString();
        }

        public static async Task<string> GetUserRoleAsync(long userId)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT role FROM user_roles WHERE user_id = @userId";
            var parameters = new Dictionary<string, object> { { "@userId", userId } };
            var result = await dbConnection.ExecuteScalarAsync(sql, parameters);
            return result?.ToString() ?? "unknown";
        }

        public static (string Username, string Password) GetCredentialsForRole(string role)
        {
            return EnvironmentVariableLoader.GetCredentialsForRole(role);
        }
        #endregion

        #region ScheduleJob
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

                    await ScheduleJob.ExecuteScheduledJob(jobName, userId, parameter);

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
        #endregion
    }
}