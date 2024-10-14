using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Services;

namespace TelegramBot.Core.Handlers
{
    public static class UpdateHandler
    {
        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                LogUpdateInfo(update);

                if (update.Type == UpdateType.Message && update.Message?.Text != null)
                {
                    LoggerService.LogDebug("Received message: {Message}", update.Message.Text);
                    await MessageHandler.HandleMessageAsync(update.Message, cancellationToken);
                }
                else if (update.Type == UpdateType.CallbackQuery)
                {
                    LoggerService.LogDebug("Received callback query: {CallbackData}", update.CallbackQuery.Data);
                    await CallbackQueryHandler.HandleCallbackQueryAsync(update.CallbackQuery, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex,
                    "Error processing update. UpdateId: {UpdateId}, Type: {UpdateType}",
                    update.Id, update.Type);
            }
        }

        private static void LogUpdateInfo(Update update)
        {
            if (update.Message?.Chat == null && update.CallbackQuery?.Message?.Chat == null)
            {
                LoggerService.LogWarning("Received update with no chat information. UpdateId: {UpdateId}", update.Id);
                return;
            }

            var chatId = update.Message?.Chat.Id ?? update.CallbackQuery.Message.Chat.Id;
            var username = update.Message?.From?.Username ?? update.CallbackQuery?.From?.Username ?? "Unknown";
            var updateType = update.Type.ToString();

            LoggerService.LogInformation(
                "Processing update. Type: {UpdateType}, UpdateId: {UpdateId}, ChatId: {ChatId}, Username: {Username}",
                updateType, update.Id, chatId, username);
        }
    }
}