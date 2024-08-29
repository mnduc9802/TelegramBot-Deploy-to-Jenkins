using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using dotenv.net;
using System.Data;
using TelegramBot.DbContext;
using TelegramBot.Models;

namespace TelegramBot.Commands
{
    public class ProjectsCommand
    {
        private static readonly string JENKINS_URL;
        private static readonly string JENKINS_USERNAME;
        private static readonly string JENKINS_PASSWORD;

        static ProjectsCommand()
        {
            DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));
            JENKINS_URL = Environment.GetEnvironmentVariable("JENKINS_URL");
            JENKINS_USERNAME = Environment.GetEnvironmentVariable("JENKINS_USERNAME");
            JENKINS_PASSWORD = Environment.GetEnvironmentVariable("JENKINS_PASSWORD");
        }

        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            try
            {
                // Lấy danh sách các dự án từ Jenkins
                var projects = await GetJenkinsProjectsAsync();
                var projectsList = "*Danh sách các dự án:*\n" + string.Join("\n", projects.Select((p, i) => $"{i + 1}. {p.Replace("_", "\\_")}"));

                // Lấy danh sách các job đang được lên lịch
                var scheduledJobs = await GetScheduledJobsAsync();
                string scheduledJobsList;

                if (scheduledJobs.Any())
                {
                    // Sắp xếp các job theo thời gian lên lịch
                    scheduledJobs = scheduledJobs.OrderBy(j => j.ScheduledTime).ToList();
                    scheduledJobsList = string.Join("\n", scheduledJobs.Select((j, index) => $"{index + 1}. {j.JobName} - {j.ScheduledTime:dd/MM/yyyy HH:mm}"));
                }
                else
                {
                    scheduledJobsList = "Chưa có job nào được lên lịch.";
                }

                // Tạo thông báo chứa cả danh sách các dự án và job đang được lên lịch
                var messageText = $"{projectsList}\n\n*Các job đang được lên lịch:*\n\n{scheduledJobsList}";

                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: messageText,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
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

        public static async Task<List<string>> GetJenkinsProjectsAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(JENKINS_URL);
                    var byteArray = Encoding.ASCII.GetBytes($"{JENKINS_USERNAME}:{JENKINS_PASSWORD}");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                    var response = await client.GetAsync("/api/json?tree=jobs[name]");
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);

                    return json["jobs"].Select(job => job["name"].ToString()).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving Jenkins projects: {ex.Message}");
                throw new Exception("Failed to retrieve Jenkins projects", ex);
            }
        }
    }
}