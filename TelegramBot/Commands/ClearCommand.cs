using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class ClearCommand
{
    private const int MaxMessagesToDelete = 100;

    public static async Task ExecuteAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        try
        {
            // Gửi tin nhắn thông báo đang xóa
            var notificationMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Đang xóa tất cả tin nhắn...",
                cancellationToken: cancellationToken);

            // Xóa tất cả tin nhắn gần đây
            await DeleteAllMessages(botClient, chatId, notificationMessage.MessageId, cancellationToken);

            // Xóa tin nhắn thông báo
            await botClient.DeleteMessageAsync(chatId, notificationMessage.MessageId, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing chat: {ex.Message}");
        }
    }

    private static async Task DeleteAllMessages(ITelegramBotClient botClient, long chatId, int notificationMessageId, CancellationToken cancellationToken)
    {
        var lastMessageId = notificationMessageId - 1; // Bắt đầu từ tin nhắn trước tin nhắn thông báo

        while (lastMessageId > 0)
        {
            try
            {
                // Xóa tin nhắn
                await botClient.DeleteMessageAsync(chatId, lastMessageId, cancellationToken);
                lastMessageId--;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting message {lastMessageId}: {ex.Message}");
                break; // Nếu gặp lỗi, dừng việc xóa tin nhắn
            }
        }
    }
}
