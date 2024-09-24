using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Commands.MinorCommands
{
    public class HelpCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var helpText = "*Các lệnh có sẵn:*\n\n" +
                           "🛠️ /start - *Khởi tạo bot* - Bắt đầu tương tác với bot.\n\n" +
                           "📂 /projects - *Danh sách các dự án* - Hiển thị tất cả các dự án hiện có.\n\n" +
                           "🚀 /deploy - *Triển khai dự án* - Triển khai dự án đã chọn.\n" +
                           "      *Lên lịch:* Trả lời tin nhắn của bot đúng định dạng, ví dụ: 09/08/2024 11:11 || Nhập hủy/cancel || Nhập df (mặc định sẽ lên lịch trong 30 phút kế tiếp).\n\n" +
                           "📈 /status - *Trạng thái hiện tại* - Xem trạng thái của bot.\n\n" +
                           "🗑️ /clear - *Xóa tin nhắn* - Xóa tất cả tin nhắn trong cuộc trò chuyện.\n\n" +
                           "📝 /feedback - *Gửi phản hồi* - Gửi phản hồi hoặc ý kiến.\n" +
                           "      *Feedback:* Trả lời tin nhắn của bot với đúng định dạng (@bot text).\n\n" +
                           "❓ /help - *Danh sách lệnh* - Hiển thị hướng dẫn sử dụng bot.\n\n" +
                           "*Chúc bạn có trải nghiệm tốt với bot của tôi!*";

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: helpText,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
    }
}