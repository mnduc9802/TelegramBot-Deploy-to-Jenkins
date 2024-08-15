using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Utilities.DeployUtilities
{
    public static class JobFinder
    {
        private static Dictionary<long, int> lastMessageIds = new Dictionary<long, int>();

        public static async Task HandleSearchCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;

            // Xóa tin nhắn chứa nút tìm kiếm
            await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);

            var sentMessage = await botClient.SendTextMessageAsync(
                chatId,
                "Vui lòng nhập tên job bạn muốn tìm kiếm:",
                replyMarkup: new ForceReplyMarkup { Selective = true },
                cancellationToken: cancellationToken
            );

            // Lưu ID của tin nhắn mới
            lastMessageIds[chatId] = sentMessage.MessageId;
        }

        public static async Task HandleSearchQuery(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var searchQuery = message.Text.ToLower();

            // Xóa tin nhắn yêu cầu nhập tên job
            if (lastMessageIds.TryGetValue(chatId, out int lastMessageId))
            {
                await botClient.DeleteMessageAsync(chatId, lastMessageId, cancellationToken);
                lastMessageIds.Remove(chatId);
            }

            // Xóa tin nhắn chứa câu truy vấn tìm kiếm của người dùng
            await botClient.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);

            if (JobPaginator.chatState.TryGetValue(chatId, out var state))
            {
                var matchingJobs = state.Jobs.Where(job => job.Name.ToLower().Contains(searchQuery)).ToList();

                if (matchingJobs.Any())
                {
                    var keyboard = JobKeyboardManager.CreateJobKeyboard(matchingJobs, 0, 1, includeBackButton: true);
                    await botClient.SendTextMessageAsync(chatId, $"Kết quả tìm kiếm cho '{searchQuery}':", replyMarkup: keyboard, cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, $"Không tìm thấy job nào phù hợp với '{searchQuery}'.", cancellationToken: cancellationToken);
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Không thể tìm thấy thông tin trạng thái. Vui lòng thử lại từ đầu.", cancellationToken: cancellationToken);
            }
        }
    }
}