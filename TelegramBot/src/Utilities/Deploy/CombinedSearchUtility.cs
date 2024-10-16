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

namespace TelegramBot.Utilities.Deploy
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

                if (lastMessageIds.TryGetValue(chatId, out int lastMessageId))
                {
                    try
                    {
                        await botClient.DeleteMessageAsync(chatId, lastMessageId, cancellationToken);
                    }
                    catch (ApiRequestException ex) when (ex.Message.Contains("message to delete not found"))
                    {
                        // Log the error but continue execution
                        Console.WriteLine($"Failed to delete message {lastMessageId} for chat {chatId}: {ex.Message}");
                    }
                    lastMessageIds.Remove(chatId);
                }

                try
                {
                    await botClient.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);
                }
                catch (ApiRequestException ex) when (ex.Message.Contains("message to delete not found"))
                {
                    // Log the error but continue execution
                    Console.WriteLine($"Failed to delete message {message.MessageId} for chat {chatId}: {ex.Message}");
                }

                var matchingJobs = new List<Job>();
                var matchingFolders = new List<string>();

                if (FolderPaginator.chatState.TryGetValue(chatId, out var rootFolders))
                {
                    foreach (var rootFolder in rootFolders)
                    {
                        await SearchJobsRecursivelyAsync(httpClient, rootFolder, searchQuery, matchingJobs, userId);
                        SearchFoldersNonRecursively(rootFolder, searchQuery, matchingFolders);
                    }
                }

                if (matchingJobs.Any() || matchingFolders.Any())
                {
                    var keyboard = CreateCombinedSearchKeyboard(matchingJobs, matchingFolders);
                    await botClient.SendTextMessageAsync(chatId, $"Kết quả tìm kiếm cho '{searchQuery}':", replyMarkup: keyboard, cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, $"Không tìm thấy job hoặc folder nào phù hợp với '{searchQuery}'.", cancellationToken: cancellationToken);
                }
            }
        }

        private static async Task SearchJobsRecursivelyAsync(HttpClient client, string currentPath, string searchQuery, List<Job> matchingJobs, long userId)
        {
            try
            {
                Console.WriteLine($"SearchJobsRecursivelyAsync - User ID: {userId}, Searching in folder: {currentPath}");

                // Set up authentication
                var userRole = await CredentialService.GetUserRoleAsync(userId);
                var (username, password) = CredentialService.GetCredentialsForRole(userRole);
                var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

                var response = await client.GetAsync($"/job/{currentPath.Replace("/", "/job/")}/api/json?tree=jobs[name,url,color]");
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"SearchJobsRecursivelyAsync - User ID: {userId}, Failed to fetch jobs for path {currentPath}. Status code: {response.StatusCode}");
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var jobs = json["jobs"] as JArray;

                if (jobs == null)
                {
                    Console.WriteLine($"SearchJobsRecursivelyAsync - User ID: {userId}, No jobs found for path {currentPath}");
                    return;
                }

                foreach (var job in jobs)
                {
                    var jobName = job["name"]?.ToString();
                    var jobUrl = job["url"]?.ToString();
                    if (jobName == null || jobUrl == null) continue;

                    var relativeUrl = jobUrl.Replace(client.BaseAddress + "job/", "").TrimEnd('/');

                    if (job["color"] != null)
                    {
                        Console.WriteLine($"SearchJobsRecursivelyAsync - User ID: {userId}, Found job: {jobName}");
                        if (jobName.ToLower().Contains(searchQuery))
                        {
                            matchingJobs.Add(new Job { JobName = jobName, Url = relativeUrl });
                        }
                    }
                    else
                    {
                        Console.WriteLine($"SearchJobsRecursivelyAsync - User ID: {userId}, Found folder: {jobName}. Searching recursively...");
                        var subFolderPath = currentPath + "/" + jobName;
                        await SearchJobsRecursivelyAsync(client, subFolderPath, searchQuery, matchingJobs, userId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SearchJobsRecursivelyAsync - User ID: {userId}, Error searching folder {currentPath}: {ex.Message}");
            }
        }

        private static void SearchFoldersNonRecursively(string currentPath, string searchQuery, List<string> matchingFolders)
        {
            var pathParts = currentPath.Split('/');
            var currentMatch = "";

            for (int i = 0; i < pathParts.Length; i++)
            {
                currentMatch += (i > 0 ? "/" : "") + pathParts[i];
                if (currentMatch.ToLower().Contains(searchQuery))
                {
                    matchingFolders.Add(currentMatch);
                    return; // Stop after finding the largest matching folder
                }
            }
        }

        //private static async Task<List<Job>> GetJobsRecursivelyAsync(HttpClient client, string currentPath, string rootPath, long userId)
        //{
        //    var result = new List<Job>();
        //    try
        //    {
        //        Console.WriteLine($"GetJobsRecursivelyAsync - User ID: {userId}, Fetching jobs for path: {currentPath}");

        //        // Get user role and credentials
        //        var userRole = await CredentialService.GetUserRoleAsync(userId);
        //        var (username, password) = CredentialService.GetCredentialsForRole(userRole);

        //        // Set up authentication
        //        var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        //        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

        //        var response = await client.GetAsync($"/job/{currentPath.Replace("/", "/job/")}/api/json?tree=jobs[name,url,color]");
        //        if (!response.IsSuccessStatusCode)
        //        {
        //            Console.WriteLine($"GetJobsRecursivelyAsync - User ID: {userId}, Failed to fetch jobs for path {currentPath}. Status code: {response.StatusCode}");
        //            return result;
        //        }
        //        var content = await response.Content.ReadAsStringAsync();
        //        var json = JObject.Parse(content);
        //        var jobs = json["jobs"] as JArray;
        //        if (jobs == null)
        //        {
        //            Console.WriteLine($"GetJobsRecursivelyAsync - User ID: {userId}, No jobs found for path {currentPath}");
        //            return result;
        //        }
        //        foreach (var job in jobs)
        //        {
        //            var jobName = job["name"]?.ToString();
        //            var jobUrl = job["url"]?.ToString();
        //            if (jobName == null || jobUrl == null) continue;
        //            var relativeUrl = jobUrl.Replace(client.BaseAddress + "job/", "").TrimEnd('/');
        //            if (job["color"] != null)
        //            {
        //                Console.WriteLine($"GetJobsRecursivelyAsync - User ID: {userId}, Found job: {jobName}");
        //                result.Add(new Job { JobName = jobName, Url = relativeUrl });
        //            }
        //            else
        //            {
        //                Console.WriteLine($"GetJobsRecursivelyAsync - User ID: {userId}, Found folder: {jobName}. Searching recursively...");
        //                var subFolderPath = currentPath + "/" + jobName;
        //                result.AddRange(await GetJobsRecursivelyAsync(client, subFolderPath, rootPath, userId));
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"GetJobsRecursivelyAsync - User ID: {userId}, Error in GetJobsRecursivelyAsync for path {currentPath}: {ex.Message}");
        //    }
        //    return result;
        //}

        private static InlineKeyboardMarkup CreateCombinedSearchKeyboard(List<Job> jobs, List<string> folders)
        {
            var keyboardButtons = new List<List<InlineKeyboardButton>>();

            foreach (var job in jobs)
            {
                var shortId = JobKeyboardManager.GenerateUniqueShortId();
                JobKeyboardManager.jobUrlMap[shortId] = job.Url;
                keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData($"🔧 {job.JobName}", $"deploy_{shortId}") });
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
