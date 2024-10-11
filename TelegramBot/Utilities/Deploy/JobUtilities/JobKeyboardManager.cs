using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Data.Models;

namespace TelegramBot.Utilities.Deploy
{
    public class JobKeyboardManager
    {
        public static Dictionary<string, string> jobUrlMap = new Dictionary<string, string>();

        public static InlineKeyboardMarkup CreateJobKeyboard(List<Job> jobs, int currentPage, int totalPages, bool includeBackToFolderButton = false, bool includeBackButton = false)
        {
            var keyboardButtons = new List<List<InlineKeyboardButton>>();

            // Job buttons
            foreach (var job in jobs)
            {
                var shortId = GenerateUniqueShortId();
                jobUrlMap[shortId] = job.Url;
                keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData(job.JobName, $"deploy_{shortId}") });
            }

            // Navigation, Search, and Back buttons on the same row
            var navigationSearchAndBackButtons = new List<InlineKeyboardButton>();
            if (currentPage > 0)
            {
                navigationSearchAndBackButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"page_{currentPage - 1}"));
            }
            navigationSearchAndBackButtons.Add(InlineKeyboardButton.WithCallbackData("🔍", "search"));
            navigationSearchAndBackButtons.Add(InlineKeyboardButton.WithCallbackData("📁", "back_to_folder"));
            if (currentPage < totalPages - 1)
            {
                navigationSearchAndBackButtons.Add(InlineKeyboardButton.WithCallbackData("➡️", $"page_{currentPage + 1}"));
            }

            // Add Back button if needed
            if (includeBackButton)
            {
                navigationSearchAndBackButtons.Add(InlineKeyboardButton.WithCallbackData("📄", "back_to_jobs"));
            }

            keyboardButtons.Add(navigationSearchAndBackButtons);

            return new InlineKeyboardMarkup(keyboardButtons);
        }

        private static string GenerateUniqueShortId()
        {
            string shortId;
            do
            {
                shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
            } while (jobUrlMap.ContainsKey(shortId));
            return shortId;
        }

        public static string? GetJobUrl(string shortId)
        {
            return jobUrlMap.TryGetValue(shortId, out var url) ? url : null;
        }
    }
}