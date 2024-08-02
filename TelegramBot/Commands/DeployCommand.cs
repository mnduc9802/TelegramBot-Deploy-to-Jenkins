using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Commands
{
    public class DeployCommand
    {
        private const string JENKINS_URL = "https://jenkins.eztek.net";
        private const string JENKINS_USERNAME = "bot";
        private const string JENKINS_PASSWORD = "1qazxsw2!@";
        public static Dictionary<string, string> jobUrlMap = new Dictionary<string, string>();
        private static Dictionary<long, (List<JobInfo> Jobs, string ProjectPath)> chatState = new Dictionary<long, (List<JobInfo>, string)>();
        private const int JOBS_PER_PAGE = 5; // Số job hiển thị trên mỗi trang

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
                chatState[message.Chat.Id] = (jobs, projectPath);
                await ShowJobsPage(botClient, message.Chat.Id, jobs, 0, projectPath, cancellationToken);
            }
        }

        private static async Task ShowJobsPage(ITelegramBotClient botClient, long chatId, List<JobInfo> jobs, int page, string projectPath, CancellationToken cancellationToken)
        {
            int startIndex = page * JOBS_PER_PAGE;
            var pageJobs = jobs.Skip(startIndex).Take(JOBS_PER_PAGE).ToList();

            jobUrlMap.Clear(); // Clear previous mappings
            var jobButtons = pageJobs.Select(job =>
            {
                var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
                jobUrlMap[shortId] = job.Url;
                return new[] { InlineKeyboardButton.WithCallbackData(job.Name, $"deploy_{shortId}") };
            }).ToList();

            // Add navigation buttons
            var navigationButtons = new List<InlineKeyboardButton>();
            if (page > 0)
            {
                navigationButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ Trước", $"page_{page - 1}"));
            }
            if (startIndex + pageJobs.Count < jobs.Count)
            {
                navigationButtons.Add(InlineKeyboardButton.WithCallbackData("Sau ➡️", $"page_{page + 1}"));
            }
            if (navigationButtons.Any())
            {
                jobButtons.Add(navigationButtons.ToArray());
            }

            var jobKeyboard = new InlineKeyboardMarkup(jobButtons);

            await botClient.SendTextMessageAsync(
                chatId,
                $"Chọn job để triển khai trong {projectPath} (Trang {page + 1}/{(jobs.Count - 1) / JOBS_PER_PAGE + 1}):",
                replyMarkup: jobKeyboard,
                cancellationToken: cancellationToken
            );
        }

        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;

            if (callbackQuery.Data.StartsWith("page_"))
            {
                if (chatState.TryGetValue(chatId, out var state))
                {
                    var page = int.Parse(callbackQuery.Data.Split('_')[1]);
                    await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
                    await ShowJobsPage(botClient, chatId, state.Jobs, page, state.ProjectPath, cancellationToken);
                }
                else
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Không thể tìm thấy thông tin trạng thái. Vui lòng thử lại từ đầu.", cancellationToken: cancellationToken);
                }
            }
            else if (callbackQuery.Data.StartsWith("deploy_"))
            {
                var shortId = callbackQuery.Data.Replace("deploy_", "");
                if (jobUrlMap.TryGetValue(shortId, out string jobUrl))
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
            // Replace multiple occurrences of "job" with a single "job" segment
            return string.Join("/job/", projectPath.Split(new[] { "/job/" }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static async Task SendDeployResultAsync(ITelegramBotClient botClient, long chatId, string project, bool success, CancellationToken cancellationToken)
        {
            var resultMessage = success ? $"Triển khai {project} thành công!" : $"Triển khai {project} thất bại.";
            await botClient.SendTextMessageAsync(chatId, resultMessage, cancellationToken: cancellationToken);
        }
    }

    public class JobInfo
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}
