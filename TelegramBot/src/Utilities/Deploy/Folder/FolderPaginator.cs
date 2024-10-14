using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Utilities.Deploy.FolderUtilities
{
    public class FolderPaginator
    {
        private const int FOLDERS_PER_PAGE = 10;
        public static Dictionary<long, List<string>> chatState = new Dictionary<long, List<string>>();

        public static async Task ShowFoldersPage(ITelegramBotClient botClient, long chatId, List<string> folders, int page, CancellationToken cancellationToken, int? messageId = null)
        {
            int startIndex = page * FOLDERS_PER_PAGE;
            var pageFolders = folders.Skip(startIndex).Take(FOLDERS_PER_PAGE).ToList();
            int totalPages = (folders.Count - 1) / FOLDERS_PER_PAGE + 1;

            var folderKeyboard = FolderKeyboardManager.CreateFolderKeyboard(pageFolders, page, totalPages);

            string message = $"Chọn dự án để triển khai (Trang {page + 1}/{totalPages}):";

            if (messageId.HasValue)
            {
                await botClient.EditMessageTextAsync(chatId, messageId.Value, message, replyMarkup: folderKeyboard, cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, message, replyMarkup: folderKeyboard, cancellationToken: cancellationToken);
            }
        }

        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            const int PAGE_INDEX = 1; // Chỉ số của phần tử trong mảng chứa số trang
            var chatId = callbackQuery.Message.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;
            if (callbackQuery.Data.StartsWith("folderpage_"))
            {
                if (chatState.TryGetValue(chatId, out var folders))
                {
                    var page = int.Parse(callbackQuery.Data.Split('_')[PAGE_INDEX]);
                    await ShowFoldersPage(botClient, chatId, folders, page, cancellationToken, messageId);
                }
                else
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Không thể tìm thấy thông tin trạng thái. Vui lòng thử lại từ đầu.", cancellationToken: cancellationToken);
                }
            }
        }
    }
}