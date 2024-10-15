using System.Net.Sockets;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using TelegramBot.Services;

namespace TelegramBot.Core.Handlers
{
    public class ErrorHandler
    {
        public static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException =>
                    $"Telegram API Error [{apiRequestException.ErrorCode}]: {apiRequestException.Message}",
                HttpRequestException httpRequestException =>
                    $"HTTP Request Error: {httpRequestException.Message}",
                SocketException socketException =>
                    $"Network Error [{socketException.SocketErrorCode}]: {socketException.Message}",
                TaskCanceledException =>
                    "Request cancelled",
                _ => exception.ToString()
            };

            LoggerService.LogError(exception, "Polling error: {ErrorMessage}", errorMessage);

            if (exception is HttpRequestException || exception is SocketException)
            {
                LoggerService.LogInformation("Will attempt to reconnect in 5 seconds...");
            }

            return Task.CompletedTask;
        }

        public static void HandleCriticalError(Exception ex)
        {
            Console.WriteLine($"Critical error during bot startup: {ex.Message}");
            Console.WriteLine("Press any key to exit.");
            Console.Read();
        }
    }
}