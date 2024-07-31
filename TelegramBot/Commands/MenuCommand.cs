using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TelegramBot.Commands
{
    public class MenuCommand
    {
        public static async Task SetBotCommandsAsync(ITelegramBotClient botClient)
        {
            var commands = new[]
            {
                new Telegram.Bot.Types.BotCommand { Command = "start", Description = "Bắt đầu sử dụng bot" },
                new Telegram.Bot.Types.BotCommand { Command = "projects", Description = "Danh sách các dự án" },
                new Telegram.Bot.Types.BotCommand { Command = "deploy", Description = "Triển khai dự án" },
                new Telegram.Bot.Types.BotCommand { Command = "status", Description = "Hiển thị trạng thái" },
                new Telegram.Bot.Types.BotCommand { Command = "clear", Description = "Xóa tất cả tin nhắn" },
                new Telegram.Bot.Types.BotCommand { Command = "feedback", Description = "Gửi phản hồi" },
                new Telegram.Bot.Types.BotCommand { Command = "help", Description = "Hiển thị trợ giúp" }
            };

            await botClient.SetMyCommandsAsync(commands);
        }
    }
}
