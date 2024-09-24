using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Commands.MinorCommands
{
    public class MenuCommand
    {
        public static async Task SetBotCommandsAsync(ITelegramBotClient botClient)
        {
            var commands = new List<BotCommand>
            {
                new BotCommand { Command = "start", Description = "Bắt đầu sử dụng bot" },
                new BotCommand { Command = "hello", Description = "Xin chào người dùng" },
                new BotCommand { Command = "deploy", Description = "Triển khai dự án" },
                new BotCommand { Command = "projects", Description = "Danh sách các dự án" },
                new BotCommand { Command = "status", Description = "Hiển thị trạng thái" },
                new BotCommand { Command = "clear", Description = "Xóa tất cả tin nhắn" },
                new BotCommand { Command = "feedback", Description = "Gửi phản hồi" },
                new BotCommand { Command = "help", Description = "Hiển thị trợ giúp" }
            };

            await botClient.SetMyCommandsAsync(commands);
        }
    }
}