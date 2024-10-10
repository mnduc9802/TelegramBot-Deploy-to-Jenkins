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
                new BotCommand { Command = "hello", Description = "Chào mừng người dùng" },
                new BotCommand { Command = "notify", Description = "Thông báo đến group chat khác" },
                new BotCommand { Command = "projects", Description = "Danh sách các dự án" },
                new BotCommand { Command = "deploy", Description = "Triển khai dự án" },
                new BotCommand { Command = "status", Description = "Trạng thái" },
                new BotCommand { Command = "clear", Description = "Xóa tất cả tin nhắn" },
                new BotCommand { Command = "feedback", Description = "Gửi phản hồi" },
                new BotCommand { Command = "help", Description = "Trợ giúp" }
            };

            await botClient.SetMyCommandsAsync(commands);
        }
    }
}