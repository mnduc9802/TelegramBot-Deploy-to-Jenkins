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
using Telegram.Bot.Exceptions;
using System.Collections.Concurrent;

namespace TelegramBot.Utilities.Deploy
{
    public static class CombinedSearchUtility
    {
        private static readonly ConcurrentDictionary<long, int> lastMessageIds = new ConcurrentDictionary<long, int>();
        private static readonly HttpClient httpClient;
        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private const int SearchTimeoutSeconds = 60;
        private const int ProgressUpdateIntervalMs = 3000;

        static CombinedSearchUtility()
        {
            string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
            httpClient = new HttpClient
            {
                BaseAddress = new Uri(jenkinsUrl),
                Timeout = TimeSpan.FromSeconds(SearchTimeoutSeconds)
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
                "Vui lòng nhập từ khóa để tìm kiếm job hoặc folder:",
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

            if (message.ReplyToMessage?.Text == "Vui lòng nhập từ khóa để tìm kiếm job hoặc folder:")
            {
                var searchQuery = message.Text.ToLower();

                if (lastMessageIds.TryRemove(chatId, out int lastMessageId))
                {
                    try
                    {
                        await botClient.DeleteMessageAsync(chatId, lastMessageId, cancellationToken);
                    }
                    catch (ApiRequestException ex) when (ex.Message.Contains("message to delete not found"))
                    {
                        Console.WriteLine($"Failed to delete message {lastMessageId} for chat {chatId}: {ex.Message}");
                    }
                }

                try
                {
                    await botClient.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);
                }
                catch (ApiRequestException ex) when (ex.Message.Contains("message to delete not found"))
                {
                    Console.WriteLine($"Failed to delete message {message.MessageId} for chat {chatId}: {ex.Message}");
                }

                var progressMessage = await botClient.SendTextMessageAsync(chatId, "Bắt đầu tìm kiếm...", cancellationToken: cancellationToken);

                var matchingJobs = new ConcurrentBag<Job>();
                var matchingFolders = new ConcurrentBag<string>();
                var searchProgress = new Progress<string>(async (update) =>
                {
                    try
                    {
                        await botClient.EditMessageTextAsync(chatId, progressMessage.MessageId, update, cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to update progress message: {ex.Message}");
                    }
                });

                if (FolderPaginator.chatState.TryGetValue(chatId, out var rootFolders))
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(SearchTimeoutSeconds)))
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
                    {
                        try
                        {
                            var searchTask = SearchAllFoldersAsync(rootFolders, searchQuery, matchingJobs, matchingFolders, userId, searchProgress, linkedCts.Token);
                            await searchTask;
                        }
                        catch (OperationCanceledException)
                        {
                            await searchProgress.OnProgressAsync("Tìm kiếm đã bị hủy hoặc hết thời gian.");
                        }
                        catch (Exception ex)
                        {
                            await searchProgress.OnProgressAsync($"Đã xảy ra lỗi trong quá trình tìm kiếm: {ex.Message}");
                        }
                    }
                }

                if (matchingJobs.Any() || matchingFolders.Any())
                {
                    var keyboard = CreateCombinedSearchKeyboard(matchingJobs.ToList(), matchingFolders.ToList());
                    await botClient.EditMessageTextAsync(chatId, progressMessage.MessageId,
                        $"Kết quả tìm kiếm cho '{searchQuery}':\nTìm thấy {matchingJobs.Count} jobs và {matchingFolders.Count} folders.",
                        replyMarkup: keyboard, cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.EditMessageTextAsync(chatId, progressMessage.MessageId,
                        $"Không tìm thấy job hoặc folder nào phù hợp với '{searchQuery}'.",
                        cancellationToken: cancellationToken);
                }
            }
        }

        private static async Task SearchAllFoldersAsync(List<string> rootFolders, string searchQuery, ConcurrentBag<Job> matchingJobs, ConcurrentBag<string> matchingFolders, long userId, IProgress<string> progress, CancellationToken cancellationToken)
        {
            var folderQueue = new ConcurrentQueue<string>(rootFolders);
            var processedRootFolders = 0;
            var totalRootFolders = rootFolders.Count;

            async Task ProcessFoldersAsync()
            {
                while (folderQueue.TryDequeue(out var folder))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await SearchFolderAsync(folder, searchQuery, matchingJobs, matchingFolders, userId, folderQueue, cancellationToken);

                    if (rootFolders.Contains(folder))
                    {
                        Interlocked.Increment(ref processedRootFolders);
                        await UpdateProgressAsync(processedRootFolders, totalRootFolders, matchingJobs.Count, matchingFolders.Count, progress);
                    }
                }
            }

            var tasks = Enumerable.Range(0, 5).Select(_ => ProcessFoldersAsync());
            await Task.WhenAll(tasks);

            // Final update to ensure we show 100% completion
            await UpdateProgressAsync(totalRootFolders, totalRootFolders, matchingJobs.Count, matchingFolders.Count, progress);
        }

        private static async Task UpdateProgressAsync(int processedFolders, int totalFolders, int jobCount, int folderCount, IProgress<string> progress)
        {
            await progress.OnProgressAsync($"Đã tìm kiếm {processedFolders}/{totalFolders} thư mục cha. Tìm thấy {jobCount} jobs và {folderCount} folders.");
        }

        private static async Task SearchFolderAsync(string folder, string searchQuery, ConcurrentBag<Job> matchingJobs, ConcurrentBag<string> matchingFolders, long userId, ConcurrentQueue<string> folderQueue, CancellationToken cancellationToken)
        {
            try
            {
                await semaphore.WaitAsync(cancellationToken);

                var userRole = await CredentialService.GetUserRoleAsync(userId);
                var (username, password) = CredentialService.GetCredentialsForRole(userRole);
                var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));

                using (var request = new HttpRequestMessage(HttpMethod.Get, $"/job/{folder.Replace("/", "/job/")}/api/json?tree=jobs[name,url,color]"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);

                    using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"SearchFolderAsync - User ID: {userId}, Failed to fetch jobs for path {folder}. Status code: {response.StatusCode}");
                            return;
                        }

                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        var json = JObject.Parse(content);
                        var jobs = json["jobs"] as JArray;

                        if (jobs == null)
                        {
                            Console.WriteLine($"SearchFolderAsync - User ID: {userId}, No jobs found for path {folder}");
                            return;
                        }

                        foreach (var job in jobs)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            var jobName = job["name"]?.ToString();
                            var jobUrl = job["url"]?.ToString();
                            if (jobName == null || jobUrl == null) continue;

                            var relativeUrl = jobUrl.Replace(httpClient.BaseAddress + "job/", "").TrimEnd('/');

                            if (job["color"] != null)
                            {
                                if (jobName.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    matchingJobs.Add(new Job { JobName = jobName, Url = relativeUrl });
                                }
                            }
                            else
                            {
                                var subFolderPath = folder + "/" + jobName;
                                folderQueue.Enqueue(subFolderPath);
                            }
                        }

                        if (folder.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchingFolders.Add(folder);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SearchFolderAsync - User ID: {userId}, Error searching folder {folder}: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static void SearchFoldersNonRecursively(string currentPath, string searchQuery, ConcurrentBag<string> matchingFolders)
        {
            var pathParts = currentPath.Split('/');
            var currentMatch = "";

            for (int i = 0; i < pathParts.Length; i++)
            {
                currentMatch += (i > 0 ? "/" : "") + pathParts[i];
                if (currentMatch.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchingFolders.Add(currentMatch);
                    return;
                }
            }
        }

        private static InlineKeyboardMarkup CreateCombinedSearchKeyboard(List<Job> jobs, List<string> folders)
    {
        var keyboardButtons = new List<List<InlineKeyboardButton>>();

        foreach (var job in jobs.Take(5))
        {
            var shortId = JobKeyboardManager.GenerateUniqueShortId();
            JobKeyboardManager.jobUrlMap[shortId] = job.Url;
            
            // Tạo tên hiển thị cho job
            var displayName = GetDisplayNameFromUrl(job.Url);
            
            keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData($"🔧 {displayName}", $"deploy_{shortId}") });
        }

        foreach (var folder in folders.Take(5))
        {
            var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
            FolderKeyboardManager.folderPathMap[shortId] = folder;
            
            // Chỉ lấy tên folder cha
            var folderName = folder.Split('/').Last();
            
            keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData($"📁 {folderName}", $"folder_{shortId}") });
        }

        keyboardButtons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🔍", "search"),
            InlineKeyboardButton.WithCallbackData("📁", "back_to_folder")
        });

        return new InlineKeyboardMarkup(keyboardButtons);
    }

    private static string GetDisplayNameFromUrl(string url)
    {
        // Tìm vị trí của "/job/" đầu tiên trong URL
        int startIndex = url.IndexOf("/job/");
        if (startIndex == -1)
        {
            // Nếu không tìm thấy "/job/", trả về URL gốc
            return url;
        }

        // Cắt chuỗi từ vị trí sau "/job/" đầu tiên
        string relevantPart = url.Substring(startIndex + 5);

        // Tách chuỗi thành các phần, loại bỏ "job" và khoảng trắng
        string[] parts = relevantPart.Split(new[] { "/job/", "/" }, StringSplitOptions.RemoveEmptyEntries);

        // Kết hợp các phần lại với nhau, sử dụng khoảng trắng làm dấu phân cách
        return string.Join(" ", parts);
    }
    }

    public static class ProgressExtensions
    {
        public static Task OnProgressAsync<T>(this IProgress<T> progress, T value)
        {
            return Task.Run(() => progress.Report(value));
        }
    }
}