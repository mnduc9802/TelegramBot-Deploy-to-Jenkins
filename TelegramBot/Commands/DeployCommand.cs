using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Utilities;

namespace TelegramBot.Commands
{
    public class DeployCommand
    {
        private const string JENKINS_URL = "https://jenkins.eztek.net";
        private const string JENKINS_USERNAME = "bot";
        private const string JENKINS_PASSWORD = "1qazxsw2!@";

        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, string projectPath, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, $"Đang chuẩn bị triển khai {projectPath}...", cancellationToken: cancellationToken);

            var jobs = await GetDeployableJobsAsync(projectPath);

            if (jobs.Count == 0)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, $"Không tìm thấy job có thể triển khai trong {projectPath}.", cancellationToken: cancellationToken);
            }
            else if (jobs.Count == 1)
            {
                var deployResult = await DeployProjectAsync(jobs[0].Url);
                await SendDeployResultAsync(botClient, message.Chat.Id, jobs[0].Name, deployResult, cancellationToken);
            }
            else
            {
                Paginator.chatState[message.Chat.Id] = (jobs, projectPath);
                await Paginator.ShowJobsPage(botClient, message.Chat.Id, jobs, 0, projectPath, cancellationToken);
            }
        }

        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;

            if (callbackQuery.Data.StartsWith("deploy_"))
            {
                var shortId = callbackQuery.Data.Replace("deploy_", "");
                if (Paginator.jobUrlMap.TryGetValue(shortId, out string jobUrl))
                {
                    var deployResult = await DeployProjectAsync(jobUrl);
                    await SendDeployResultAsync(botClient, chatId, jobUrl, deployResult, cancellationToken);
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Đã bắt đầu triển khai job", cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Không tìm thấy thông tin job", cancellationToken: cancellationToken);
                }
            }
            else
            {
                await Paginator.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
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

        private static async Task<bool> DeployProjectAsync(string projectPath)
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

                var normalizedProjectPath = NormalizeJenkinsPath(projectPath);
                var url = $"/job/{normalizedProjectPath}/build";
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

        private static string NormalizeJenkinsPath(string projectPath)
        {
            return string.Join("/job/", projectPath.Split(new[] { "/job/" }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static async Task SendDeployResultAsync(ITelegramBotClient botClient, long chatId, string project, bool success, CancellationToken cancellationToken)
        {
            var resultMessage = success ? $"Triển khai {project} thành công!" : $"Triển khai {project} thất bại.";
            await botClient.SendTextMessageAsync(chatId, resultMessage, cancellationToken: cancellationToken);
        }
    }
}