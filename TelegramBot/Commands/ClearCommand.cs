﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Commands
{
    public class ClearCommand
    {
        public static async Task RequestConfirmationAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
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

        public static async Task ExecuteAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            try
            {
                // Gửi tin nhắn thông báo đang xóa
                var notificationMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Đang xóa tất cả tin nhắn...",
                    cancellationToken: cancellationToken);

                // Xóa tất cả tin nhắn gần đây
                await DeleteAllMessagesAsync(botClient, chatId, notificationMessage.MessageId, cancellationToken);

                // Xóa tin nhắn thông báo
                await botClient.DeleteMessageAsync(chatId, notificationMessage.MessageId, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing chat: {ex.Message}");
            }
        }

        private static async Task DeleteAllMessagesAsync(ITelegramBotClient botClient, long chatId, int notificationMessageId, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            bool keepDeleting = true;

            for (int messageId = notificationMessageId - 1; messageId > 0 && keepDeleting; messageId--)
            {
                tasks.Add(DeleteMessageSafeAsync(botClient, chatId, messageId, cancellationToken, () => keepDeleting = false));
            }

            await Task.WhenAll(tasks);
        }

        private static async Task DeleteMessageSafeAsync(ITelegramBotClient botClient, long chatId, int messageId, CancellationToken cancellationToken, Action stopDeleting)
        {
            try
            {
                await botClient.DeleteMessageAsync(chatId, messageId, cancellationToken);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("message to delete not found"))
                {
                    stopDeleting();
                }
                else
                {
                    Console.WriteLine($"Error deleting message {messageId}: {ex.Message}");
                }
            }
        }
    }
}
