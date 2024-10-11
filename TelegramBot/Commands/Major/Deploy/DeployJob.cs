using System.Text;
using System.Net.Http.Headers;
using Telegram.Bot;
using Telegram.Bot.Types;
using Newtonsoft.Json.Linq;
using TelegramBot.Data.Models;
using TelegramBot.Services;
using TelegramBot.Utilities.Deploy;
using TelegramBot.Utilities.Environment;

namespace TelegramBot.Commands.Major.Deploy
{
    public class DeployJob
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, string projectPath, long userId, CancellationToken cancellationToken)
        {
            Console.WriteLine($"DeployJob.ExecuteAsync starting - UserId: {userId}, ProjectPath: {projectPath}");

            var userRole = await CredentialService.GetUserRoleAsync(userId);
            Console.WriteLine($"DeployJob.ExecuteAsync - Retrieved role for userId: {userId}, Role: {userRole}");

            var initialMessage = await botClient.SendTextMessageAsync(
                message.Chat.Id,
                $"Đang chuẩn bị triển khai {projectPath}...",
                cancellationToken: cancellationToken
            );

            try
            {
                var jobs = await GetDeployableJobsAsync(projectPath, userRole, userId);
                Console.WriteLine($"DeployJob.ExecuteAsync - Found {jobs.Count} jobs for userId: {userId}");

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
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeployJob.ExecuteAsync for userId {userId}: {ex.Message}");
                throw;
            }
        }

        private static async Task<List<Job>> GetDeployableJobsAsync(string folderPath, string userRole, long userId)
        {
            string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
            using var client = new HttpClient { BaseAddress = new Uri(jenkinsUrl) };
            var credentials = CredentialService.GetCredentialsForRole(userRole);
            var byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return await GetJobsRecursivelyAsync(client, folderPath, folderPath);
        }

        public static async Task<bool> DeployProjectAsync(string projectPath, string userRole, long userId, string? parameter = null)
        {
            var folderPath = Path.GetDirectoryName(projectPath)?.Replace("job/", "");
            var jobName = Path.GetFileName(projectPath);
            LoggerService.LogInformation("Starting deployment. JobName: {JobName}, FolderPath: {FolderPath}, Parameter: {Parameter}, UserId: {UserId}, UserRole: {UserRole}", jobName, folderPath, parameter ?? "none", userId, userRole);

            try
            {
                string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
                using var client = new HttpClient { BaseAddress = new Uri(jenkinsUrl) };
                var credentials = EnvironmentVariableLoader.GetCredentialsForRole(userRole);
                var byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                Console.WriteLine($"Attempting to deploy project: {projectPath} for userId: {userId}");

                var crumbResponse = await client.GetAsync("/crumbIssuer/api/json");
                if (!crumbResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to get crumb. Status code: {crumbResponse.StatusCode}");
                    return false;
                }

                var crumbJson = JObject.Parse(await crumbResponse.Content.ReadAsStringAsync());
                var crumbRequestField = crumbJson["crumbRequestField"]?.ToString();
                var crumb = crumbJson["crumb"]?.ToString();

                if (crumbRequestField != null && crumb != null)
                {
                    client.DefaultRequestHeaders.Add(crumbRequestField, crumb);
                }

                var url = $"/job/{projectPath.Replace("/", "/")}/build";
                if (!string.IsNullOrEmpty(parameter))
                {
                    url += $"WithParameters?VERSION={parameter}";
                }

                LoggerService.LogDebug("Sending deploy request. JobName: {JobName}, FolderPath: {FolderPath}, Url: {Url}", jobName, folderPath, url);
                var response = await client.PostAsync(url, null);

                if (!response.IsSuccessStatusCode)
                {
                    LoggerService.LogWarning("Deployment failed. JobName: {JobName}, FolderPath: {FolderPath}, StatusCode: {StatusCode}, Response: {Response}", jobName, folderPath, response.StatusCode, await response.Content.ReadAsStringAsync());
                    return false;
                }
                else
                {
                    LoggerService.LogInformation("Deployment request sent successfully. JobName: {JobName}, FolderPath: {FolderPath}, UserId: {UserId}", jobName, folderPath, userId);

                    await JobService.ResetJobsTableIfNeeded();

                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, "Deployment failed. JobName: {JobName}, FolderPath: {FolderPath}, UserId: {UserId}", jobName, folderPath, userId);
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

            var jobs = json["jobs"] as JArray;
            if (jobs == null) return result;

            foreach (var job in jobs)
            {
                var jobName = job["name"]?.ToString();
                var jobUrl = job["url"]?.ToString();
                if (jobName == null || jobUrl == null) continue;

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