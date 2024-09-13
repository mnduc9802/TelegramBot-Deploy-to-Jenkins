using System;
using System.Data;
using System.Timers;
using TelegramBot.Commands;
using TelegramBot.DbContext;
using TelegramBot;

namespace TelegramBot.Utilities
{
    public class ScheduledJobManager
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
                var sql = "SELECT job_name, scheduled_time, user_id, parameter FROM scheduled_jobs WHERE scheduled_time <= @now";
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
                Console.WriteLine($"Error in CheckScheduledJobs: {ex.Message}");
            }
        }

        private static async Task ExecuteScheduledJob(string jobName, long userId, string parameter)
        {
            try
            {
                Console.WriteLine($"Attempting to deploy job: {jobName} for user {userId} with parameter: {parameter ?? "None"}");
                var userRole = await ProjectsCommand.GetUserRoleAsync(userId);
                var deployResult = await DeployCommand.DeployProjectAsync(jobName, userRole, parameter);
                Console.WriteLine($"Deploy result for {jobName}: {deployResult}");

                // TODO: Notify the user about the deployment result
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing scheduled job {jobName}: {ex.Message}");
            }
        }
    }
}