using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

public class HelpCommand
{
    public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var helpText = "Các lệnh có sẵn:\n" +
                        "/start - Bắt đầu chương trình\n" +
                        "/clear - Xóa toàn bộ tin nhắn\n" +
                        "/help - Hiển thị danh sách các lệnh";

        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: helpText,
            cancellationToken: cancellationToken);
    }
}
