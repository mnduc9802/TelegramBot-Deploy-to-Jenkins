using System.Net;
using System.Text.Json;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Services
{
    public class JenkinsNotificationService
    {
        private readonly HttpListener _listener;
        private readonly ConcurrentDictionary<string, ChatId> _activeBuilds;
        private readonly ITelegramBotClient _botClient;

        public JenkinsNotificationService(ITelegramBotClient botClient, string webhookUrl)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(webhookUrl);
            _activeBuilds = new ConcurrentDictionary<string, ChatId>();
            _botClient = botClient;
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine("Jenkins Notification Service started.");
            ListenForWebhooks();
        }

        private async void ListenForWebhooks()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessWebhookAsync(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in webhook listener: {ex.Message}");
                }
            }
        }

        private async Task ProcessWebhookAsync(HttpListenerContext context)
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            string jsonPayload = await reader.ReadToEndAsync();

            try
            {
                var jenkinsEvent = JsonSerializer.Deserialize<JenkinsEvent>(jsonPayload);
                if (jenkinsEvent != null && _activeBuilds.TryRemove(jenkinsEvent.Name, out var chatId))
                {
                    string message = $"Job {jenkinsEvent.Name} {jenkinsEvent.Status}.\nBuild URL: {jenkinsEvent.Url}";
                    await _botClient.SendTextMessageAsync(chatId, message);
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing Jenkins webhook: {ex.Message}");
            }

            context.Response.StatusCode = 200;
            context.Response.Close();
        }

        public void AddActiveBuild(string jobName, ChatId chatId)
        {
            _activeBuilds[jobName] = chatId;
        }
    }

    public class JenkinsEvent
    {
        public string? Name { get; set; }
        public string? Status { get; set; }
        public string? Url { get; set; }
    }
}