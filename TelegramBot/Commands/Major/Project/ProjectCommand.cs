using System.Data;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Services;

namespace TelegramBot.Commands.MajorCommands.ProjectCommand
{
    public class ProjectCommand
    {
        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            try
            {
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Danh sách các dự án đang triển khai", "show_projects")
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Danh sách các job đã lên lịch", "show_scheduled_jobs")
                    }
                });

                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Chọn một tùy chọn:",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Có lỗi xảy ra: {ex.Message}",
                    cancellationToken: cancellationToken);
            }
        }

        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;

            switch (callbackQuery.Data)
            {
                case "show_projects":
                    await ShowProjects(botClient, chatId, callbackQuery.From.Id, cancellationToken);
                    break;
                case "show_scheduled_jobs":
                    await ShowScheduledJobs(botClient, chatId, cancellationToken);
                    break;
                case string s when s.StartsWith("edit_job_"):
                    await ListScheduleJob.EditJobTime(botClient, chatId, messageId, s.Replace("edit_job_", ""), cancellationToken);
                    break;
                case string s when s.StartsWith("delete_job_"):
                    await ListScheduleJob.DeleteJob(botClient, chatId, messageId, s.Replace("delete_job_", ""), cancellationToken);
                    break;
            }
        }

        public static async Task ShowProjects(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
        {
            try
            {
                var userRole = await CredentialService.GetUserRoleAsync(userId);
                var projects = await JenkinsProject.GetJenkinsProjectsAsync(userId, userRole);
                var projectsList = "*Danh sách các dự án:*\n" + string.Join("\n", projects.Select((p, i) => $"{i + 1}. {p.Replace("_", "\\_")}"));

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: projectsList,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Có lỗi xảy ra khi lấy danh sách dự án: {ex.Message}",
                    cancellationToken: cancellationToken);
            }
        }

        public static async Task ShowScheduledJobs(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var scheduledJobs = await ListScheduleJob.GetScheduledJobsAsync();

            if (!scheduledJobs.Any())
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Không có job nào được lên lịch.",
                    cancellationToken: cancellationToken);
                return;
            }

            var jobList = string.Join("\n", scheduledJobs.Select((job, index) =>
                $"{index + 1}. {job.JobName} - {job.ScheduledTime:dd/MM/yyyy HH:mm}"));

            var jobButtons = scheduledJobs.Select(job => new[]
            {
                InlineKeyboardButton.WithCallbackData($"Sửa {job.JobName}", $"edit_job_{job.JobName}"),
                InlineKeyboardButton.WithCallbackData($"Xóa {job.JobName}", $"delete_job_{job.JobName}")
            }).ToList();


            var navigationButtons = new List<InlineKeyboardButton[]>
            {
                new[] { InlineKeyboardButton.WithCallbackData("📁", "back_to_folder") }
            };

            var keyboard = new InlineKeyboardMarkup(jobButtons.Concat(navigationButtons));

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Danh sách các job đã lên lịch:\n\n{jobList}",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
    }
}