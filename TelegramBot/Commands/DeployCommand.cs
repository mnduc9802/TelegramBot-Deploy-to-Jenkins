using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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

        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, string projectPath, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, $"Đang chuẩn bị triển khai {projectPath}...", cancellationToken: cancellationToken);

            var jobs = await GetJobsInFolderAsync(projectPath);

            if (jobs.Count == 0)
            {
                var deployResult = await DeployProjectAsync(projectPath);
                await SendDeployResultAsync(botClient, message.Chat.Id, projectPath, deployResult, cancellationToken);
            }
            else
            {
                var jobButtons = jobs.Select(job => new[] { InlineKeyboardButton.WithCallbackData(job, $"deploy_{projectPath}/{job}") }).ToList();
                var jobKeyboard = new InlineKeyboardMarkup(jobButtons);

                await botClient.SendTextMessageAsync(message.Chat.Id, $"Chọn job để triển khai trong {projectPath}:", replyMarkup: jobKeyboard, cancellationToken: cancellationToken);
            }
        }

        private static async Task<List<string>> GetJobsInFolderAsync(string folderPath)
        {
            using var client = new HttpClient { BaseAddress = new Uri(JENKINS_URL) };
            var byteArray = Encoding.ASCII.GetBytes($"{JENKINS_USERNAME}:{JENKINS_PASSWORD}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            var response = await client.GetAsync($"/job/{folderPath}/api/json?tree=jobs[name]");

            if (!response.IsSuccessStatusCode) return new List<string>();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            return json["jobs"]?.Select(job => job["name"].ToString()).ToList() ?? new List<string>();
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

                var jobPath = projectPath.Replace("/", "/job/");
                var url = $"/job/{jobPath}/build";
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

        private static async Task SendDeployResultAsync(ITelegramBotClient botClient, long chatId, string project, bool success, CancellationToken cancellationToken)
        {
            var resultMessage = success ? $"Triển khai {project} thành công!" : $"Triển khai {project} thất bại.";
            await botClient.SendTextMessageAsync(chatId, resultMessage, cancellationToken: cancellationToken);
        }
    }
}
