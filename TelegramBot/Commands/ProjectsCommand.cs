using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

public class ProjectsCommand
{
    public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var projectsList = "Danh sách các dự án:\n" +
                            "1. Project A\n" +
                            "2. Project B\n" +
                            "3. Project C";

        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: projectsList,
            cancellationToken: cancellationToken);
    }
}
