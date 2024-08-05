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

            // Navigation and Search buttons on the same row
            var navigationAndSearchButtons = new List<InlineKeyboardButton>();
            if (currentPage > 0)
            {
                navigationAndSearchButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"page_{currentPage - 1}"));
            }
            navigationAndSearchButtons.Add(InlineKeyboardButton.WithCallbackData("🔍", "search"));
            if (currentPage < totalPages - 1)
            {
                navigationAndSearchButtons.Add(InlineKeyboardButton.WithCallbackData("➡️", $"page_{currentPage + 1}"));
            }
            keyboardButtons.Add(navigationAndSearchButtons);

            // Go Back and Exit buttons
            var goBackAndExitButtons = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("🔙 Go Back", "go_back"),
                InlineKeyboardButton.WithCallbackData("❌ Exit", "exit")
            };
            keyboardButtons.Add(goBackAndExitButtons);

            return new InlineKeyboardMarkup(keyboardButtons);
        }
    }
}
