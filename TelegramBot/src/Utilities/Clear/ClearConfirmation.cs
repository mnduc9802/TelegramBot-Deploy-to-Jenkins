using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Utilities.ClearUtilities
{
    public static class ClearConfirmation
    {
        public static async Task ClearConfirmationKeyboard(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Có", "clear_yes"),
                    InlineKeyboardButton.WithCallbackData("Không", "clear_no")
                }
            });

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Bạn có chắc chắn muốn xóa tất cả tin nhắn không?",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }

        public static async Task HandleConfirmationCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Func<Task> onConfirm, Func<Task> onCancel, CancellationToken cancellationToken)
        {
            var callbackData = callbackQuery.Data;

            if (callbackData == "clear_yes")
            {
                await onConfirm();
            }
            else if (callbackData == "clear_no")
            {
                await onCancel();
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        }
    }
}