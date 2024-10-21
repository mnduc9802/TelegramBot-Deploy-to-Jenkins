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
using System.Collections.Concurrent;
using System.Linq;

namespace TelegramBot.Utilities.Deploy.General
{
    public static class Finder
    {
        private static readonly ConcurrentDictionary<long, int> lastMessageIds = new ConcurrentDictionary<long, int>();
        private static readonly HttpClient httpClient;
        private static readonly LRUCache<string, (List<Job> Jobs, List<string> Folders)> searchCache = new LRUCache<string, (List<Job>, List<string>)>(100, TimeSpan.FromDays(1));
        private const int RESULTS_PER_PAGE = 5;
        private static readonly ConcurrentDictionary<long, (string Query, List<Job> Jobs, List<string> Folders)> searchState = new ConcurrentDictionary<long, (string, List<Job>, List<string>)>();

        static Finder()
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

            if (message.ReplyToMessage?.Text == "Vui lòng trả lời tin nhắn này để tìm kiếm job hoặc folder:")
            {
                var searchQuery = message.Text.ToLower();

                if (searchCache.TryGetValue(searchQuery, out var cachedResult))
                {
                    searchState[chatId] = (searchQuery, cachedResult.Jobs, cachedResult.Folders);
                    await SendSearchResultsPage(botClient, chatId, 0, cancellationToken);
                    return;
                }

                var matchingJobs = new ConcurrentBag<Job>();
                var matchingFolders = new ConcurrentDictionary<string, byte>();

                if (FolderPaginator.chatState.TryGetValue(chatId, out var rootFolders))
                {
                    await Task.WhenAll(rootFolders.Select(rootFolder =>
                        SearchRecursivelyAsync(httpClient, rootFolder, searchQuery, matchingJobs, matchingFolders, userId, "", 5)
                    ));
                }

                var resultJobs = matchingJobs.ToList();
                var resultFolders = matchingFolders.Keys.ToList();

                searchCache.Add(searchQuery, (resultJobs, resultFolders));
                searchState[chatId] = (searchQuery, resultJobs, resultFolders);

                await SendSearchResultsPage(botClient, chatId, 0, cancellationToken);
            }
        }

        private static async Task SearchRecursivelyAsync(HttpClient client, string currentPath, string searchQuery, ConcurrentBag<Job> matchingJobs, ConcurrentDictionary<string, byte> matchingFolders, long userId, string parentPath, int depth)
        {
            if (depth <= 0) return;

            try
            {
                string fullPath = string.IsNullOrEmpty(parentPath) ? currentPath : $"{parentPath}/{currentPath}";
                bool folderMatches = fullPath.ToLower().Contains(searchQuery);

                var userRole = await CredentialService.GetUserRoleAsync(userId);
                var (username, password) = CredentialService.GetCredentialsForRole(userRole);
                var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

                var response = await client.GetAsync($"/job/{fullPath.Replace("/", "/job/")}/api/json?tree=jobs[name,url,color]");
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var jobs = json["jobs"] as JArray;

                if (jobs == null)
                {
                    return;
                }

                bool addedToMatchingFolders = false;

                var tasks = new List<Task>();

                foreach (var job in jobs)
                {
                    var jobName = job["name"]?.ToString();
                    var jobUrl = job["url"]?.ToString();
                    if (jobName == null || jobUrl == null) continue;

                    var relativeUrl = jobUrl.Replace(client.BaseAddress + "job/", "").TrimEnd('/');

                    if (job["color"] != null)
                    {
                        if (jobName.ToLower().Contains(searchQuery) || folderMatches)
                        {
                            matchingJobs.Add(new Job { JobName = jobName, Url = relativeUrl, FullPath = fullPath });
                            if (folderMatches && !addedToMatchingFolders)
                            {
                                matchingFolders.TryAdd(fullPath, 0);
                                addedToMatchingFolders = true;
                            }
                        }
                    }
                    else
                    {
                        tasks.Add(SearchRecursivelyAsync(client, jobName, searchQuery, matchingJobs, matchingFolders, userId, fullPath, depth - 1));
                    }
                }

                await Task.WhenAll(tasks);

                if (folderMatches && !addedToMatchingFolders)
                {
                    matchingFolders.TryAdd(fullPath, 0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SearchRecursivelyAsync - User ID: {userId}, Error searching folder {currentPath}: {ex.Message}");
            }
        }

        private static async Task SendSearchResultsPage(ITelegramBotClient botClient, long chatId, int page, CancellationToken cancellationToken)
        {
            if (searchState.TryGetValue(chatId, out var state))
            {
                var (searchQuery, jobs, folders) = state;
                var allResults = jobs.Select(j => (Item: (object)j, IsJob: true))
                                     .Concat(folders.Select(f => (Item: (object)f, IsJob: false)))
                                     .ToList();

                int totalPages = (allResults.Count + RESULTS_PER_PAGE - 1) / RESULTS_PER_PAGE;
                var pageResults = allResults.Skip(page * RESULTS_PER_PAGE).Take(RESULTS_PER_PAGE).ToList();

                if (pageResults.Any())
                {
                    var keyboard = CreateSearchKeyboard(pageResults, page, totalPages);
                    await botClient.SendTextMessageAsync(
                        chatId,
                        $"Kết quả tìm kiếm cho '{searchQuery}' (Trang {page + 1}/{totalPages}):",
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    await botClient.SendTextMessageAsync(
                        chatId,
                        $"Không tìm thấy job hoặc folder nào phù hợp với '{searchQuery}'.",
                        cancellationToken: cancellationToken
                    );
                }
            }
        }

        private static InlineKeyboardMarkup CreateSearchKeyboard(List<(object Item, bool IsJob)> results, int currentPage, int totalPages)
        {
            var keyboardButtons = new List<List<InlineKeyboardButton>>();
            foreach (var result in results)
            {
                if (result.IsJob)
                {
                    var job = (Job)result.Item;
                    var shortId = JobKeyboardManager.GenerateUniqueShortId();
                    JobKeyboardManager.jobUrlMap[shortId] = job.Url;
                    keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData($"🔧 {job.FullPath} {job.JobName}", $"deploy_{shortId}") });
                }
                else
                {
                    var folder = (string)result.Item;
                    var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
                    FolderKeyboardManager.folderPathMap[shortId] = folder;
                    keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData($"📁 {folder}", $"folder_{shortId}") });
                }
            }

            var lastRow = new List<InlineKeyboardButton>();

            if (currentPage > 0)
            {
                lastRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"searchpage_{currentPage - 1}"));
            }

            lastRow.Add(InlineKeyboardButton.WithCallbackData("🔍", "search"));
            lastRow.Add(InlineKeyboardButton.WithCallbackData("📁", "back_to_folder"));

            if (currentPage < totalPages - 1)
            {
                lastRow.Add(InlineKeyboardButton.WithCallbackData("➡️", $"searchpage_{currentPage + 1}"));
            }

            keyboardButtons.Add(lastRow);

            return new InlineKeyboardMarkup(keyboardButtons);
        }

        public static async Task HandleSearchPaginationCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;

            if (callbackQuery.Data.StartsWith("searchpage_"))
            {
                var page = int.Parse(callbackQuery.Data.Split('_')[1]);
                await UpdateSearchResultsPage(botClient, chatId, messageId, page, cancellationToken);
            }
        }

        private static async Task UpdateSearchResultsPage(ITelegramBotClient botClient, long chatId, int messageId, int page, CancellationToken cancellationToken)
        {
            if (searchState.TryGetValue(chatId, out var state))
            {
                var (searchQuery, jobs, folders) = state;
                var allResults = jobs.Select(j => (Item: (object)j, IsJob: true))
                                     .Concat(folders.Select(f => (Item: (object)f, IsJob: false)))
                                     .ToList();

                int totalPages = (allResults.Count + RESULTS_PER_PAGE - 1) / RESULTS_PER_PAGE;
                var pageResults = allResults.Skip(page * RESULTS_PER_PAGE).Take(RESULTS_PER_PAGE).ToList();

                if (pageResults.Any())
                {
                    var keyboard = CreateSearchKeyboard(pageResults, page, totalPages);
                    await botClient.EditMessageTextAsync(
                        chatId,
                        messageId,
                        $"Kết quả tìm kiếm cho '{searchQuery}' (Trang {page + 1}/{totalPages}):",
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    await botClient.EditMessageTextAsync(
                        chatId,
                        messageId,
                        $"Không tìm thấy job hoặc folder nào phù hợp với '{searchQuery}'.",
                        cancellationToken: cancellationToken
                    );
                }
            }
        }
    }
}