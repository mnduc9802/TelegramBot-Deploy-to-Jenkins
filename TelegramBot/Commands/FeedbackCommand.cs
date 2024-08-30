using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.DbContext;
using dotenv.net;

namespace TelegramBot.Commands
{
    public class FeedbackCommand
    {
        private static readonly DatabaseConnection _dbConnection;
        private static readonly long _myChatId;

        static FeedbackCommand()
        {
            var envVars = DotEnv.Read(options: new DotEnvOptions(probeForEnv: true));
            string connectionString = envVars["DATABASE_CONNECTION_STRING"];
            _dbConnection = new DatabaseConnection(connectionString);

            _myChatId = long.Parse(envVars["MY_TELEGRAM_CHAT_ID"]);
        }

        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var feedbackText = "Vui lòng gửi phản hồi của bạn bằng cách trả lời tin nhắn này với định dạng @bot text.";

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: feedbackText,
                cancellationToken: cancellationToken);
        }

        public static async Task HandleFeedbackResponseAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var userId = message.From.Id;
            var firstName = message.From.FirstName ?? "Không có tên";
            var lastName = message.From.LastName ?? "";
            var userName = string.IsNullOrEmpty(message.From.Username) ? $"{firstName} {lastName}" : message.From.Username;

            // Loại bỏ tên bot khỏi nội dung phản hồi
            var feedbackText = message.Text.Replace("@mnduc9802_deploy_bot", "").Trim();

            await SaveFeedbackToDatabase(userId, userName, feedbackText);

            var feedbackNotification = $"Phản hồi mới từ người dùng {firstName} {lastName} (Username: {userName}, ID: {userId}):\n{feedbackText}";

            await botClient.SendTextMessageAsync(
                chatId: _myChatId,
                text: feedbackNotification,
                cancellationToken: cancellationToken
            );

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Cảm ơn bạn đã phản hồi dịch vụ của tôi. Phản hồi của bạn đã được lưu lại.",
                cancellationToken: cancellationToken
            );
        }

        private static async Task SaveFeedbackToDatabase(long userId, string userName, string feedbackText)
        {
            string sql = "INSERT INTO user_feedback (user_id, user_name, feedback_text, created_at) VALUES (@userId, @userName, @feedbackText, @createdAt)";

            var parameters = new Dictionary<string, object>
            {
                { "@userId", userId },
                { "@userName", userName },
                { "@feedbackText", feedbackText },
                { "@createdAt", DateTime.UtcNow }
            };

            await _dbConnection.ExecuteNonQueryAsync(sql, parameters);
        }
    }
}