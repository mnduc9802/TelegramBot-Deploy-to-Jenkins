using dotenv.net;
using System;

namespace TelegramBot.Utilities.EnvironmentUtilities
{
    public static class EnvironmentVariableLoader
    {
        public static string GetJenkinsUrl()
        {
            DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));
            var jenkinsUrl = Environment.GetEnvironmentVariable("JENKINS_URL");
            return jenkinsUrl ?? throw new InvalidOperationException("JENKINS_URL environment variable is not set.");
        }

        public static (string Username, string Password) GetCredentialsForRole(string role)
        {
            DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));
            return role.ToLower() switch
            {
                "devops" => GetCredentials("DEVOPS"),
                "developer" => GetCredentials("DEVELOPER"),
                "tester" => GetCredentials("TESTER"),
                _ => GetCredentials("DEVELOPER") // Default to developer credentials
            };
        }

        private static (string Username, string Password) GetCredentials(string prefix)
        {
            var username = Environment.GetEnvironmentVariable($"{prefix}_USERNAME");
            var password = Environment.GetEnvironmentVariable($"{prefix}_PASSWORD");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException($"{prefix} credentials are not properly set in the environment variables.");
            }

            return (username, password);
        }
    }
}