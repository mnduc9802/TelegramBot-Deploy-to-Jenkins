using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using dotenv.net;

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

            Console.WriteLine($"JENKINS_URL: {JENKINS_URL}");
            Console.WriteLine($"JENKINS_USERNAME: {JENKINS_USERNAME}");
            Console.WriteLine($"JENKINS_PASSWORD: {JENKINS_PASSWORD}");
        }

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