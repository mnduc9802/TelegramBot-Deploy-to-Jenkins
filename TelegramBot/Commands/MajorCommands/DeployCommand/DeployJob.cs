using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot;
using TelegramBot.Utilities.DeployUtilities;
using System.Net.Http.Headers;
using TelegramBot.Models;
using Newtonsoft.Json.Linq;
using TelegramBot.Utilities.EnvironmentUtilities;
using TelegramBot.Services;

namespace TelegramBot.Commands.MajorCommands.DeployCommand
{
    public class DeployJob
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, string projectPath, CancellationToken cancellationToken)
        {
            var userId = message.From.Id;
            var userRole = await CredentialService.GetUserRoleAsync(userId);

            var initialMessage = await botClient.SendTextMessageAsync(
                message.Chat.Id,
                $"Đang chuẩn bị triển khai {projectPath}...",
                cancellationToken: cancellationToken
            );

            var jobs = await GetDeployableJobsAsync(projectPath, userRole);

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
                var jobUrlId = await JobService.GetOrCreateJobUrlId(jobs[0].Url, userId);
                await DeployConfirmation.JobDeployConfirmationKeyboard(
                    botClient,
                    message.Chat.Id,
                    jobUrlId.ToString(),
                    jobs[0].JobName.EndsWith("_parameter"),
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

        private static async Task<List<Job>> GetDeployableJobsAsync(string folderPath, string userRole)
        {
            string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
            using var client = new HttpClient { BaseAddress = new Uri(jenkinsUrl) };
            var credentials = CredentialService.GetCredentialsForRole(userRole);
            var byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return await GetJobsRecursivelyAsync(client, folderPath, folderPath);
        }

        public static async Task<bool> DeployProjectAsync(string projectPath, string userRole, string parameter = null)
        {
            try
            {
                string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
                using var client = new HttpClient { BaseAddress = new Uri(jenkinsUrl) };
                var credentials = EnvironmentVariableLoader.GetCredentialsForRole(userRole);
                var byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                Console.WriteLine($"Attempting to deploy project: {projectPath}");

                var crumbResponse = await client.GetAsync("/crumbIssuer/api/json");
                if (!crumbResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to get crumb. Status code: {crumbResponse.StatusCode}");
                    return false;
                }

                var crumbJson = JObject.Parse(await crumbResponse.Content.ReadAsStringAsync());
                client.DefaultRequestHeaders.Add(crumbJson["crumbRequestField"].ToString(), crumbJson["crumb"].ToString());

                var url = $"/job/{projectPath.Replace("/", "/")}/build";
                if (!string.IsNullOrEmpty(parameter))
                {
                    url += $"WithParameters?VERSION={parameter}";
                }

                Console.WriteLine($"Sending request to: {client.BaseAddress}{url}");
                var response = await client.PostAsync(url, null);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Deployment failed. Status code: {response.StatusCode}");
                    Console.WriteLine($"Response content: {await response.Content.ReadAsStringAsync()}");
                    return false;
                }
                else
                {
                    Console.WriteLine("Deployment request sent successfully.");

                    await JobService.ResetJobsTableIfNeeded();

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deployment failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private static async Task<List<Job>> GetJobsRecursivelyAsync(HttpClient client, string currentPath, string rootPath)
        {
            var result = new List<Job>();

            var response = await client.GetAsync($"/job/{currentPath.Replace("/", "/job/")}/api/json?tree=jobs[name,url,color]");

            if (!response.IsSuccessStatusCode) return result;

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            foreach (var job in json["jobs"])
            {
                var jobName = job["name"].ToString();
                var jobUrl = job["url"].ToString();
                string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
                var relativeUrl = jobUrl.Replace(jenkinsUrl + "/job/", "").TrimEnd('/');

                if (job["color"] != null)
                {
                    result.Add(new Job { JobName = jobName, Url = relativeUrl });
                }
                else
                {
                    var subFolderPath = currentPath + "/" + jobName;
                    result.AddRange(await GetJobsRecursivelyAsync(client, subFolderPath, rootPath));
                }
            }

            return result;
        }
    }
}