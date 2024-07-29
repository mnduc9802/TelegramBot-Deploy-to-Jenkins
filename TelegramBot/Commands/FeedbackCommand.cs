using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

public class FeedbackCommand
{
    public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var feedbackText = "Vui lòng gửi phản hồi của bạn bằng cách trả lời tin nhắn này.";

        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: feedbackText,
            cancellationToken: cancellationToken);
    }
    public static async Task HandleFeedbackResponseAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "Cảm ơn bạn đã phản hồi dịch vụ của chúng tôi.",
            cancellationToken: cancellationToken
        );
    }
}
