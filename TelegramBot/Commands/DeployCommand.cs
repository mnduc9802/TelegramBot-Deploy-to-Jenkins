using System.Data;
using System.Net.Http.Headers;
using System.Text;
using dotenv.net;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.DbContext;
using TelegramBot.Models;
using TelegramBot.Utilities;
using TelegramBot.Utilities.DeployUtilities;

namespace TelegramBot.Commands
{
    public class DeployCommand
    {
        private static readonly string JENKINS_URL;
        private static readonly string JENKINS_USERNAME;
        private static readonly string JENKINS_PASSWORD;

        static DeployCommand()
        {
            DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));
            JENKINS_URL = Environment.GetEnvironmentVariable("JENKINS_URL");
            JENKINS_USERNAME = Environment.GetEnvironmentVariable("JENKINS_USERNAME");
            JENKINS_PASSWORD = Environment.GetEnvironmentVariable("JENKINS_PASSWORD");
        }

        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, string projectPath, CancellationToken cancellationToken)
        {
            var initialMessage = await botClient.SendTextMessageAsync(
                message.Chat.Id,
                $"Đang chuẩn bị triển khai {projectPath}...",
                cancellationToken: cancellationToken
            );

            var jobs = await GetDeployableJobsAsync(projectPath);

            if (jobs.Count == 0)
            {
                await botClient.DeleteMessageAsync(message.Chat.Id, initialMessage.MessageId, cancellationToken);
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    $"Không tìm thấy job có thể triển khai trong {projectPath}.",
                    cancellationToken: cancellationToken
                );
            }
            else if (jobs.Count == 1)
            {
                await botClient.DeleteMessageAsync(message.Chat.Id, initialMessage.MessageId, cancellationToken);
                await DeployConfirmation.JobDeployConfirmationKeyboard(
                    botClient,
                    message.Chat.Id,
                    jobs[0].Url,
                    cancellationToken
                );
            }
            else
            {
                await botClient.DeleteMessageAsync(message.Chat.Id, initialMessage.MessageId, cancellationToken);
                JobPaginator.chatState[message.Chat.Id] = (jobs, projectPath);
                await JobPaginator.ShowJobsPage(
                    botClient,
                    message.Chat.Id,
                    jobs,
                    0,
                    projectPath,
                    cancellationToken
                );
            }
        }

        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;

            if (callbackQuery.Data.StartsWith("deploy_"))
            {
                var shortId = callbackQuery.Data.Replace("deploy_", "");
                if (JobKeyboardManager.jobUrlMap.TryGetValue(shortId, out string jobUrl))
                {
                    await DeployConfirmation.JobDeployConfirmationKeyboard(botClient, chatId, jobUrl, cancellationToken, callbackQuery.Message.MessageId);
                }
                else
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Không tìm thấy thông tin job", cancellationToken: cancellationToken);
                }
            }
            else if (callbackQuery.Data.StartsWith("confirm_job_yes_"))
            {
                var jobUrl = callbackQuery.Data.Replace("confirm_job_yes_", "");
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
                var deployResult = await DeployProjectAsync(jobUrl);
                await SendDeployResultAsync(botClient, chatId, jobUrl, deployResult, cancellationToken);
            }
            else if (callbackQuery.Data == "confirm_job_no")
            {
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
                await botClient.SendTextMessageAsync(chatId, "Yêu cầu triển khai job đã bị hủy.", cancellationToken: cancellationToken);
            }
            else
            {
                await JobPaginator.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
        }

        private static async Task<List<JobInfo>> GetDeployableJobsAsync(string folderPath)
        {
            using var client = new HttpClient { BaseAddress = new Uri(JENKINS_URL) };
            var byteArray = Encoding.ASCII.GetBytes($"{JENKINS_USERNAME}:{JENKINS_PASSWORD}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            return await GetJobsRecursivelyAsync(client, folderPath, folderPath);
        }

        private static async Task<List<JobInfo>> GetJobsRecursivelyAsync(HttpClient client, string currentPath, string rootPath)
        {
            var result = new List<JobInfo>();

            var response = await client.GetAsync($"/job/{currentPath.Replace("/", "/job/")}/api/json?tree=jobs[name,url,color]");

            if (!response.IsSuccessStatusCode) return result;

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            foreach (var job in json["jobs"])
            {
                var jobName = job["name"].ToString();
                var jobUrl = job["url"].ToString();
                var relativeUrl = jobUrl.Replace(JENKINS_URL + "/job/", "").TrimEnd('/');

                if (job["color"] != null)
                {
                    result.Add(new JobInfo { Name = jobName, Url = relativeUrl });
                }
                else
                {
                    var subFolderPath = currentPath + "/" + jobName;
                    result.AddRange(await GetJobsRecursivelyAsync(client, subFolderPath, rootPath));
                }
            }

            return result;
        }

        public static async Task<bool> DeployProjectAsync(string projectPath)
        {
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(JENKINS_URL) };
                var byteArray = Encoding.ASCII.GetBytes($"{JENKINS_USERNAME}:{JENKINS_PASSWORD}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                var crumbResponse = await client.GetAsync("/crumbIssuer/api/json");
                if (!crumbResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to get crumb. Status code: {crumbResponse.StatusCode}");
                    return false;
                }

                var crumbJson = JObject.Parse(await crumbResponse.Content.ReadAsStringAsync());
                client.DefaultRequestHeaders.Add(crumbJson["crumbRequestField"].ToString(), crumbJson["crumb"].ToString());

                // Sửa đổi cách xử lý đường dẫn
                var url = $"/job/{projectPath.Replace("/", "/")}/build";
                Console.WriteLine($"Sending request to: {client.BaseAddress}{url}");

                var response = await client.PostAsync(url, null);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Deployment failed. Status code: {response.StatusCode}");
                    Console.WriteLine($"Response content: {await response.Content.ReadAsStringAsync()}");
                }
                else
                {
                    Console.WriteLine("Deployment request sent successfully.");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deployment failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<List<ScheduledJob>> GetScheduledJobsAsync()
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT job_name, scheduled_time, created_at FROM scheduled_jobs ORDER BY scheduled_time";
            var dataTable = await dbConnection.ExecuteReaderAsync(sql);

            var scheduledJobs = new List<ScheduledJob>();
            foreach (DataRow row in dataTable.Rows)
            {
                scheduledJobs.Add(new ScheduledJob
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
            var jobUrl = callbackQuery.Data.Replace("schedule_job_", "");
            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);

            var scheduledJobs = await GetScheduledJobsAsync();
            var jobList = string.Join("\n", scheduledJobs.Select(j => $"{j.JobName} - {j.ScheduledTime:dd/MM/yyyy HH:mm}"));

            var message = $"Các job đang được lên lịch:\n\n{jobList}\n\nVui lòng nhập thời gian bạn muốn lên lịch triển khai job (định dạng: DD/MM/YYYY HH:mm)";

            await botClient.SendTextMessageAsync(
                callbackQuery.Message.Chat.Id,
                message,
                cancellationToken: cancellationToken);

            Program.schedulingState[callbackQuery.Message.Chat.Id] = jobUrl;
        }

        private static async Task RequestScheduleTimeAsync(ITelegramBotClient botClient, long chatId, string jobUrl, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "Vui lòng nhập thời gian bạn muốn lên lịch triển khai job (định dạng: DD/MM/YYYY HH:mm)",
                cancellationToken: cancellationToken);

            Program.schedulingState[chatId] = jobUrl;
        }

        public static async Task HandleScheduleTimeInputAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            string messageText = message.Text.ToLower().Trim();

            if (messageText.Contains("hủy") || messageText.Contains("cancel"))
            {
                Program.schedulingState.TryRemove(message.Chat.Id, out _);
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Lệnh lên lịch đã bị hủy. Vui lòng /deploy để triển khai lại.",
                    cancellationToken: cancellationToken);
                return;
            }

            if (DateTime.TryParseExact(message.Text, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime scheduledTime))
            {
                if (scheduledTime <= DateTime.Now)
                {
                    await botClient.SendTextMessageAsync(
                        message.Chat.Id,
                        "Thời gian lên lịch phải là trong tương lai. Vui lòng thử lại.",
                        cancellationToken: cancellationToken);
                    return;
                }

                string jobUrl = Program.schedulingState[message.Chat.Id];
                string jobName = jobUrl.TrimEnd('/');

                var dbConnection = new DatabaseConnection(Program.connectionString);
                var sql = "INSERT INTO scheduled_jobs (job_name, scheduled_time, created_at) VALUES (@jobName, @scheduledTime, @createdAt)";
                var parameters = new Dictionary<string, object>
                {
                    { "@jobName", jobName },
                    { "@scheduledTime", scheduledTime },
                    { "@createdAt", DateTime.Now }
                };
                await dbConnection.ExecuteNonQueryAsync(sql, parameters);

                Console.WriteLine($"Scheduled job {jobName} for {scheduledTime}"); // Log để debug

                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    $"Đã lên lịch triển khai job {jobName} vào lúc {scheduledTime:dd/MM/yyyy HH:mm}.",
                    cancellationToken: cancellationToken);

                Program.schedulingState.TryRemove(message.Chat.Id, out _);
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Định dạng thời gian không hợp lệ. Vui lòng nhập lại theo định dạng DD/MM/YYYY HH:mm",
                    cancellationToken: cancellationToken);
            }
        }

        private static string NormalizeJenkinsPath(string projectPath)
        {
            return string.Join("/job/", projectPath.Split(new[] { "job/" }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static async Task SendDeployResultAsync(ITelegramBotClient botClient, long chatId, string project, bool success, CancellationToken cancellationToken)
        {
            var resultMessage = success ? $"Triển khai {project} thành công!" : $"Triển khai {project} thất bại.";
            await botClient.SendTextMessageAsync(chatId, resultMessage, cancellationToken: cancellationToken);
        }
    }
}