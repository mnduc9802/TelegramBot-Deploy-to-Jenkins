using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;

namespace TelegramBot.Utilities
{
    public class Paginator
    {
        private const int JOBS_PER_PAGE = 5;
        public static Dictionary<string, string> jobUrlMap = new Dictionary<string, string>();
        public static Dictionary<long, (List<JobInfo> Jobs, string ProjectPath)> chatState = new Dictionary<long, (List<JobInfo>, string)>();

        public static async Task ShowJobsPage(ITelegramBotClient botClient, long chatId, List<JobInfo> jobs, int page, string projectPath, CancellationToken cancellationToken, int? messageId = null)
        {
            int startIndex = page * JOBS_PER_PAGE;
            var pageJobs = jobs.Skip(startIndex).Take(JOBS_PER_PAGE).ToList();

            jobUrlMap.Clear();
            var jobButtons = pageJobs.Select(job =>
            {
                var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
                jobUrlMap[shortId] = job.Url;
                return new[] { InlineKeyboardButton.WithCallbackData(job.Name, $"deploy_{shortId}") };
            }).ToList();

            // Add navigation buttons
            var navigationButtons = new List<InlineKeyboardButton>();
            if (page > 0)
            {
                navigationButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ Trước", $"page_{page - 1}"));
            }
            if (startIndex + pageJobs.Count < jobs.Count)
            {
                navigationButtons.Add(InlineKeyboardButton.WithCallbackData("Sau ➡️", $"page_{page + 1}"));
            }
            if (navigationButtons.Any())
            {
                jobButtons.Add(navigationButtons.ToArray());
            }

            var jobKeyboard = new InlineKeyboardMarkup(jobButtons);

            if (messageId.HasValue)
            {
                await botClient.EditMessageTextAsync(
                    chatId,
                    messageId.Value,
                    $"Chọn job để triển khai trong {projectPath} (Trang {page + 1}/{(jobs.Count - 1) / JOBS_PER_PAGE + 1}):",
                    replyMarkup: jobKeyboard,
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId,
                    $"Chọn job để triển khai trong {projectPath} (Trang {page + 1}/{(jobs.Count - 1) / JOBS_PER_PAGE + 1}):",
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

    public class JobInfo
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}
