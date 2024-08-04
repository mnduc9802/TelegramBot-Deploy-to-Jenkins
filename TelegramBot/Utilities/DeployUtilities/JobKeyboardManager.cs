using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Utilities.DeployUtilities
{
    public class JobKeyboardManager
    {
        public static Dictionary<string, string> jobUrlMap = new Dictionary<string, string>();

        public static InlineKeyboardMarkup CreateJobKeyboard(List<JobInfo> jobs, int currentPage, int totalPages)
        {
            var keyboardButtons = new List<List<InlineKeyboardButton>>();

            // Job buttons
            foreach (var job in jobs)
            {
                var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
                jobUrlMap[shortId] = job.Url;
                keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData(job.Name, $"deploy_{shortId}") });
            }

            // Navigation buttons
            var navigationButtons = new List<InlineKeyboardButton>();
            if (currentPage > 0)
            {
                navigationButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ Trước", $"page_{currentPage - 1}"));
            }
            if (currentPage < totalPages - 1)
            {
                navigationButtons.Add(InlineKeyboardButton.WithCallbackData("Sau ➡️", $"page_{currentPage + 1}"));
            }
            if (navigationButtons.Any())
            {
                keyboardButtons.Add(navigationButtons);
            }

            // Search button
            keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData("🔍 Tìm kiếm", "search") });

            return new InlineKeyboardMarkup(keyboardButtons);
        }
    }
}