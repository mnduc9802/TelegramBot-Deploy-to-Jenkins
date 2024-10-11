using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Commands.Minor
{
    public class HelpCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var helpText = "*Các lệnh có sẵn:*\n\n" +
                           "👋 /hello - *Chào mừng* - Chào mừng và bắt đầu cuộc trò chuyện với bot.\n\n"+
                           "📂 /project - *Danh sách các dự án* - Hiển thị tất cả các dự án hiện có.\n" +
                           "      *Danh sách các Job đang được lên lịch:* - Hiển thị danh sách Job đang được lên lịch, có thể Sửa thời gian lên lịch || Xóa - Hủy yêu cầu lên lịch. \n\n" +
                           "🚀 /deploy - *Triển khai dự án* - Triển khai dự án đã chọn.\n" +
                           "      *Lên lịch:* Trả lời tin nhắn của bot đúng định dạng, ví dụ: 09/08/2024 11:11 || Nhập hủy/cancel || Nhập df (mặc định sẽ lên lịch trong 30 phút kế tiếp).\n\n" +
                           "🧑‍ /myinfo - *Thông tin cá nhân* - Hiển thị thông tin cá nhân.\n\n" +
                           "📈 /status - *Trạng thái hiện tại* - Xem trạng thái của bot.\n\n" +
                           "🗑️ /clear - *Xóa tin nhắn* - Xóa tất cả tin nhắn trong cuộc trò chuyện.\n\n" +
                           "🔔 /notify - *Thông báo đến group chat khác* - Chat một đoạn thông báo cho group chat khác.\n\n" +
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