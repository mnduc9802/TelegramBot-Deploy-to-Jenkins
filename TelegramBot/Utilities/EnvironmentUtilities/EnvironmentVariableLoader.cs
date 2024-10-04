using dotenv.net;

namespace TelegramBot.Utilities.EnvironmentUtilities
{
    public static class EnvironmentVariableLoader
    {
        static EnvironmentVariableLoader()
        {
            DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));
        }
        private static string GetEnvironmentVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return value ?? throw new InvalidOperationException($"{name} environment variable is not set.");
        }

        public static string GetTelegramBotToken()
        {
            return GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        }

        public static string GetDatabaseConnectionString()
        {
            return GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        }

        public static string GetJenkinsUrl()
        {
            return GetEnvironmentVariable("JENKINS_URL");
        }

        public static (string Username, string Password) GetCredentialsForRole(string role)
        {
            return role.ToLower() switch
            {
                "devops" => GetCredentials("DEVOPS"),
                "developer" => GetCredentials("DEVELOPER"),
                "tester" => GetCredentials("TESTER"),
                _ => GetCredentials("DEVELOPER") // Default to developer credentials
            };
        }

        public static (string Username, string Password) GetCredentials(string prefix)
        {
            var username = Environment.GetEnvironmentVariable($"{prefix}_USERNAME");
            var password = Environment.GetEnvironmentVariable($"{prefix}_PASSWORD");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException($"{prefix} credentials are not properly set in the environment variables.");
            }

            return (username, password);
        }

        public static long GetMyTelegramChatId()
        {
            var chatIdString = GetEnvironmentVariable("MY_TELEGRAM_CHAT_ID");
            if (!long.TryParse(chatIdString, out long chatId))
            {
                throw new InvalidOperationException("MY_TELEGRAM_CHAT_ID is not a valid long number.");
            }
            return chatId;
        }
    }
}