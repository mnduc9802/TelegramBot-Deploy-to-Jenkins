using Telegram.Bot.Types;

namespace TelegramBot.Services
{
    public class UserService
    {
        public static string GetUserIdentifier(User user)
        {
            if (!string.IsNullOrEmpty(user.Username))
            {
                return $"@{user.Username}";
            }
            else if (!string.IsNullOrEmpty(user.FirstName) || !string.IsNullOrEmpty(user.LastName))
            {
                return $"{user.FirstName} {user.LastName}".Trim();
            }
            else
            {
                return $"User {user.Id}";
            }
        }

        public static string GetFullName(User user)
        {
            return $"{user.FirstName} {user.LastName}".Trim();
        }
    }
}