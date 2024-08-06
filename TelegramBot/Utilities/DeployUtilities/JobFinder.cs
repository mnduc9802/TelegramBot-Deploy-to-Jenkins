using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Utilities.DeployUtilities
{
    public static class JobFinder
    {
        public static async Task HandleSearchCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(
                callbackQuery.Message.Chat.Id,
                "Vui lòng nhập tên job bạn muốn tìm kiếm:",
                replyMarkup: new ForceReplyMarkup { Selective = true },
                cancellationToken: cancellationToken
            );
        }

        public static async Task HandleSearchQuery(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var searchQuery = message.Text.ToLower();

            if (JobPaginator.chatState.TryGetValue(chatId, out var state))
            {
                var matchingJobs = state.Jobs.Where(job => job.Name.ToLower().Contains(searchQuery)).ToList();

                if (matchingJobs.Any())
                {
                    await JobPaginator.ShowJobsPage(botClient, chatId, matchingJobs, 0, state.ProjectPath, cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Không tìm thấy job nào phù hợp.", cancellationToken: cancellationToken);
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Không thể tìm thấy thông tin trạng thái. Vui lòng thử lại từ đầu.", cancellationToken: cancellationToken);
            }
        }
    }
}