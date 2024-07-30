using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Commands
{
    public class HelpCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var helpText = "Các lệnh có sẵn:\n" +
                           "/start - Khởi tạo bot\n" +
                           "/clear - Xóa tất cả tin nhắn\n" +
                           "/help - Hiển thị danh sách lệnh\n" +
                           "/status - Xem trạng thái hiện tại\n" +
                           "/projects - Xem danh sách các dự án\n" +
                           "/feedback - Gửi phản hồi";

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: helpText,
                cancellationToken: cancellationToken);
        }
    }
}
