using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Models;
using TelegramBot.Utilities.DeployUtilities;

namespace TelegramBot.Utilities
{
    public class JobPaginator
    {
        private const int JOBS_PER_PAGE = 5;
        public static Dictionary<long, (List<JobInfo> Jobs, string ProjectPath)> chatState = new Dictionary<long, (List<JobInfo>, string)>();

        public static async Task ShowJobsPage(ITelegramBotClient botClient, long chatId, List<JobInfo> jobs, int page, string projectPath, CancellationToken cancellationToken, int? messageId = null)
        {
            int startIndex = page * JOBS_PER_PAGE;
            var pageJobs = jobs.Skip(startIndex).Take(JOBS_PER_PAGE).ToList();
            int totalPages = (jobs.Count - 1) / JOBS_PER_PAGE + 1;

            var jobKeyboard = JobKeyboardManager.CreateJobKeyboard(pageJobs, page, totalPages);

            string message = $"Chọn job để triển khai trong {projectPath} (Trang {page + 1}/{totalPages}):";

            if (messageId.HasValue)
            {
                await botClient.EditMessageTextAsync(
                    chatId,
                    messageId.Value,
                    message,
                    replyMarkup: jobKeyboard,
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId,
                    message,
                    replyMarkup: jobKeyboard,
                    cancellationToken: cancellationToken
                );
            }
        }

        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;
            if (callbackQuery.Data.StartsWith("page_"))
            {
                if (chatState.TryGetValue(chatId, out var state))
                {
                    var page = int.Parse(callbackQuery.Data.Split('_')[1]);
                    await ShowJobsPage(botClient, chatId, state.Jobs, page, state.ProjectPath, cancellationToken, messageId);
                }
                else
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Không thể tìm thấy thông tin trạng thái. Vui lòng thử lại từ đầu.", cancellationToken: cancellationToken);
                }
            }
        }
    }
}