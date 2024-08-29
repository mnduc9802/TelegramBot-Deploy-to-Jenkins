using System.Data;
using TelegramBot.Commands;
using TelegramBot.DbContext;
using TelegramBot;

namespace TelegramBot.Utilities
{
    public class ScheduledJobManager
    {
        private static Timer timer;

        public static void Initialize()
        {
            // Kiểm tra job mỗi phút
            timer = new Timer(CheckScheduledJobs, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        private static async void CheckScheduledJobs(object state)
        {
            try
            {
                var now = DateTime.Now;
                var dbConnection = new DatabaseConnection(Program.connectionString);
                var sql = "SELECT job_name, scheduled_time FROM scheduled_jobs WHERE scheduled_time <= @now";
                var parameters = new Dictionary<string, object> { { "@now", now } };
                var dataTable = await dbConnection.ExecuteReaderAsync(sql, parameters);

                foreach (DataRow row in dataTable.Rows)
                {
                    var jobName = row["job_name"].ToString();
                    var scheduledTime = Convert.ToDateTime(row["scheduled_time"]);
                    Console.WriteLine($"Executing scheduled job: {jobName} at {scheduledTime}"); // Log để debug

                    await ExecuteScheduledJob(jobName);

                    // Xóa job đã thực hiện khỏi database
                    var deleteSql = "DELETE FROM scheduled_jobs WHERE job_name = @jobName AND scheduled_time = @scheduledTime";
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
                Console.WriteLine($"Error in CheckScheduledJobs: {ex.Message}"); // Log lỗi
            }
        }

        private static async Task ExecuteScheduledJob(string jobName)
        {
            try
            {
                Console.WriteLine($"Attempting to deploy job: {jobName}"); // Log để debug
                var deployResult = await DeployCommand.DeployProjectAsync(jobName);
                Console.WriteLine($"Deploy result for {jobName}: {deployResult}"); // Log kết quả
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing scheduled job {jobName}: {ex.Message}"); // Log lỗi
            }
        }
    }
}