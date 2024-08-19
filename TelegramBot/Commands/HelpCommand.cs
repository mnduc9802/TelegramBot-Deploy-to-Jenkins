using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Commands
{
    public class HelpCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var helpText = "*Các lệnh có sẵn:*\n\n" +
                           "🛠️ /start - *Khởi tạo bot* - Bắt đầu tương tác với bot.\n" +
                           "📂 /projects - *Danh sách các dự án* - Hiển thị tất cả các dự án hiện có.\n" +
                           "🚀 /deploy - *Triển khai dự án* - Triển khai dự án đã chọn.\n" +
                           "📈 /status - *Trạng thái hiện tại* - Xem trạng thái của bot.\n" +
                           "🗑️ /clear - *Xóa tin nhắn* - Xóa tất cả tin nhắn trong cuộc trò chuyện.\n" +
                           "📝 /feedback - *Gửi phản hồi* - Gửi phản hồi hoặc ý kiến.\n" +
                           "❓ /help - *Danh sách lệnh* - Hiển thị hướng dẫn sử dụng bot.\n\n"+
                           "*Chúc bạn có trải nghiệm tốt với bot của tôi!*";

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: helpText,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
    }
}