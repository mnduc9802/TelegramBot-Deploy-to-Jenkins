using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Utilities.Deploy.FolderUtilities
{
    public static class FolderFinder
    {
        private static Dictionary<long, int> lastMessageIds = new Dictionary<long, int>();

        public static async Task HandleSearchCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;

            await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);

            var sentMessage = await botClient.SendTextMessageAsync(
                chatId,
                "Vui lòng nhập tên dự án bạn muốn tìm kiếm:",
                replyMarkup: new ForceReplyMarkup { Selective = true },
                cancellationToken: cancellationToken
            );

            lastMessageIds[chatId] = sentMessage.MessageId;
        }

        public static async Task HandleSearchQuery(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;

            if (message.ReplyToMessage?.Text == "Vui lòng nhập tên dự án bạn muốn tìm kiếm:")
            {
                var searchQuery = message.Text.ToLower();

                if (lastMessageIds.TryGetValue(chatId, out int lastMessageId))
                {
                    await botClient.DeleteMessageAsync(chatId, lastMessageId, cancellationToken);
                    lastMessageIds.Remove(chatId);
                }

                await botClient.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);

                if (FolderPaginator.chatState.TryGetValue(chatId, out var folders))
                {
                    var matchingFolders = folders.Where(folder => folder.ToLower().Contains(searchQuery)).ToList();

                    if (matchingFolders.Any())
                    {
                        var keyboard = FolderKeyboardManager.CreateFolderKeyboard(matchingFolders, 0, 1);
                        await botClient.SendTextMessageAsync(chatId, $"Kết quả tìm kiếm cho '{searchQuery}':", replyMarkup: keyboard, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, $"Không tìm thấy dự án nào phù hợp với '{searchQuery}'.", cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Không thể tìm thấy thông tin trạng thái. Vui lòng thử lại từ đầu.", cancellationToken: cancellationToken);
                }
            }
        }
    }
}