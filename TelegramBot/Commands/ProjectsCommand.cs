using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Commands
{
    public class ProjectsCommand
    {
        private const string JENKINS_URL = "https://jenkins.eztek.net";
        private const string JENKINS_USERNAME = "*******";
        private const string JENKINS_PASSWORD = "*******";

        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            try
            {
                var projects = await GetJenkinsProjectsAsync();
                var projectsList = "Danh sách các dự án:\n" + string.Join("\n", projects.Select((p, i) => $"{i + 1}. {p}"));

                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: projectsList,
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

        public static async Task<List<string>> GetJenkinsProjectsAsync()
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
    }
}
