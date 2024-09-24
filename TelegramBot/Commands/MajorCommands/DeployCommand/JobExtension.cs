using System.Data;
using System.Timers;
using TelegramBot.DbContext;
using TelegramBot.Models;
using TelegramBot.Utilities.EnvironmentUtilities;

namespace TelegramBot.Commands.MajorCommands.DeployCommand
{
    public class JobExtension
    {
        #region DeployJob
        public static async Task<int> GetOrCreateJobUrlId(string url, long userId, string parameter = null)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();

            // Xử lý URL một cách an toàn hơn
            string path = url;
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                path = uri.AbsolutePath.TrimStart('/');
            }

            if (path.StartsWith("job/", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(4);
            }

            var selectSql = "SELECT id FROM jobs WHERE url = @url";
            var selectParams = new Dictionary<string, object> { { "@url", path } };
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
                return jobId;
            }

            // Job doesn't exist, create a new one
            var insertSql = "INSERT INTO jobs (job_name, url, user_id, created_at, parameter) VALUES (@job_name, @url, @user_id, @created_at, @parameter) RETURNING id";
            var insertParams = new Dictionary<string, object>
            {
                { "@job_name", path },
                { "@url", path },
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
        #endregion
    }
}