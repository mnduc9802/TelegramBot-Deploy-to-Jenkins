using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Data.Models;
using TelegramBot.Utilities.Deploy.FolderUtilities;
using TelegramBot.Utilities.Deploy.JobUtilities;
using TelegramBot.Utilities.Environment;
using System.Net.Http.Headers;
using System.Text;
using TelegramBot.Services;

namespace TelegramBot.Utilities.Deploy.General
{
    public static class CombinedSearchUtility
    {
        private static Dictionary<long, int> lastMessageIds = new Dictionary<long, int>();
        private static HttpClient httpClient;

        static CombinedSearchUtility()
        {
            string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
            httpClient = new HttpClient
            {
                BaseAddress = new Uri(jenkinsUrl)
            };
        }

        public static async Task HandleSearchCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var userId = callbackQuery.From.Id;

            Console.WriteLine($"HandleSearchCallback - User ID: {userId}, Chat ID: {chatId}");

            await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);

            var sentMessage = await botClient.SendTextMessageAsync(
                chatId,
                "Vui lòng trả lời tin nhắn này để tìm kiếm job hoặc folder:",
                replyMarkup: new ForceReplyMarkup { Selective = true },
                cancellationToken: cancellationToken
            );

            lastMessageIds[chatId] = sentMessage.MessageId;
        }

        public static async Task HandleSearchQuery(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var userId = message.From.Id;

            Console.WriteLine($"HandleSearchQuery - User ID: {userId}, Chat ID: {chatId}, Query: {message.Text}");

            if (message.ReplyToMessage?.Text == "Vui lòng trả lời tin nhắn này để tìm kiếm job hoặc folder:")
            {
                var searchQuery = message.Text.ToLower();

                // Delete previous messages...

                var matchingJobs = new List<Job>();
                var matchingFolders = new HashSet<string>();

                if (FolderPaginator.chatState.TryGetValue(chatId, out var rootFolders))
                {
                    foreach (var rootFolder in rootFolders)
                    {
                        await SearchRecursivelyAsync(httpClient, rootFolder, searchQuery, matchingJobs, matchingFolders, userId, "");
                    }
                }

                if (matchingJobs.Any() || matchingFolders.Any())
                {
                    var keyboard = CreateCombinedSearchKeyboard(matchingJobs, matchingFolders.ToList());
                    await botClient.SendTextMessageAsync(chatId, $"Kết quả tìm kiếm cho '{searchQuery}':", replyMarkup: keyboard, cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, $"Không tìm thấy job hoặc folder nào phù hợp với '{searchQuery}'.", cancellationToken: cancellationToken);
                }
            }
        }

        private static async Task SearchRecursivelyAsync(HttpClient client, string currentPath, string searchQuery, List<Job> matchingJobs, HashSet<string> matchingFolders, long userId, string parentPath)
        {
            try
            {
                Console.WriteLine($"SearchRecursivelyAsync - User ID: {userId}, Searching in folder: {currentPath}");

                string fullPath = string.IsNullOrEmpty(parentPath) ? currentPath : $"{parentPath}/{currentPath}";
                bool folderMatches = fullPath.ToLower().Contains(searchQuery);

                // Thiết lập xác thực
                var userRole = await CredentialService.GetUserRoleAsync(userId);
                var (username, password) = CredentialService.GetCredentialsForRole(userRole);
                var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

                var response = await client.GetAsync($"/job/{fullPath.Replace("/", "/job/")}/api/json?tree=jobs[name,url,color]");
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"SearchRecursivelyAsync - User ID: {userId}, Failed to fetch jobs for path {fullPath}. Status code: {response.StatusCode}");
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var jobs = json["jobs"] as JArray;

                if (jobs == null)
                {
                    Console.WriteLine($"SearchRecursivelyAsync - User ID: {userId}, No jobs found for path {fullPath}");
                    return;
                }

                bool addedToMatchingFolders = false;

                foreach (var job in jobs)
                {
                    var jobName = job["name"]?.ToString();
                    var jobUrl = job["url"]?.ToString();
                    if (jobName == null || jobUrl == null) continue;

                    var relativeUrl = jobUrl.Replace(client.BaseAddress + "job/", "").TrimEnd('/');

                    if (job["color"] != null)
                    {
                        Console.WriteLine($"SearchRecursivelyAsync - User ID: {userId}, Found job: {jobName}");
                        if (jobName.ToLower().Contains(searchQuery) || folderMatches)
                        {
                            matchingJobs.Add(new Job { JobName = jobName, Url = relativeUrl, FullPath = fullPath });
                            if (folderMatches && !addedToMatchingFolders)
                            {
                                matchingFolders.Add(fullPath);
                                addedToMatchingFolders = true;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"SearchRecursivelyAsync - User ID: {userId}, Found folder: {jobName}. Searching recursively...");
                        await SearchRecursivelyAsync(client, jobName, searchQuery, matchingJobs, matchingFolders, userId, fullPath);
                    }
                }

                // If this folder matches and we haven't added it yet (no matching jobs), add it now
                if (folderMatches && !addedToMatchingFolders)
                {
                    matchingFolders.Add(fullPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SearchRecursivelyAsync - User ID: {userId}, Error searching folder {currentPath}: {ex.Message}");
            }
        }

        private static InlineKeyboardMarkup CreateCombinedSearchKeyboard(List<Job> jobs, List<string> folders)
        {
            var keyboardButtons = new List<List<InlineKeyboardButton>>();

            foreach (var job in jobs)
            {
                var shortId = JobKeyboardManager.GenerateUniqueShortId();
                JobKeyboardManager.jobUrlMap[shortId] = job.Url;
                keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData($"🔧 {job.FullPath} {job.JobName}", $"deploy_{shortId}") });
            }

            foreach (var folder in folders)
            {
                var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
                FolderKeyboardManager.folderPathMap[shortId] = folder;
                keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData($"📁 {folder}", $"folder_{shortId}") });
            }

            keyboardButtons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("🔍", "search"),
                InlineKeyboardButton.WithCallbackData("📁", "back_to_folder")
            });

            return new InlineKeyboardMarkup(keyboardButtons);
        }
    }
}