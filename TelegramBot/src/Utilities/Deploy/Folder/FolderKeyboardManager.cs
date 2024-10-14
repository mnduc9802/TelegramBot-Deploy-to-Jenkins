using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Utilities.Deploy.FolderUtilities
{
    public class FolderKeyboardManager
    {
        public static Dictionary<string, string> folderPathMap = new Dictionary<string, string>();

        public static InlineKeyboardMarkup CreateFolderKeyboard(List<string> folders, int currentPage, int totalPages)
        {
            var keyboardButtons = new List<List<InlineKeyboardButton>>();

            // Folder buttons
            foreach (var folder in folders)
            {
                var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
                folderPathMap[shortId] = folder;
                keyboardButtons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData(folder, $"folder_{shortId}") });
            }

            // Navigation and Search buttons
            var navigationSearchAndBackButtons = new List<InlineKeyboardButton>();
            if (currentPage > 0)
            {
                navigationSearchAndBackButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"folderpage_{currentPage - 1}"));
            }
            navigationSearchAndBackButtons.Add(InlineKeyboardButton.WithCallbackData("🔍", "foldersearch"));
            navigationSearchAndBackButtons.Add(InlineKeyboardButton.WithCallbackData("📁", "back_to_folder"));
            if (currentPage < totalPages - 1)
            {
                navigationSearchAndBackButtons.Add(InlineKeyboardButton.WithCallbackData("➡️", $"folderpage_{currentPage + 1}"));
            }

            keyboardButtons.Add(navigationSearchAndBackButtons);

            return new InlineKeyboardMarkup(keyboardButtons);
        }
    }
}