using dotenv.net;

namespace TelegramBot.Utilities
{
    public static class EnvironmentVariableLoader
    {
        public static string GetJenkinsUrl()
        {
            DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));
            var jenkinsUrl = Environment.GetEnvironmentVariable("JENKINS_URL");
            Console.WriteLine($"Loaded JENKINS_URL: {jenkinsUrl}");
            return jenkinsUrl;
        }

        public static (string Username, string Password) GetCredentialsForRole(string role)
        {
            DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));
            return role.ToLower() switch
            {
                "devops" => (Environment.GetEnvironmentVariable("DEVOPS_USERNAME"), Environment.GetEnvironmentVariable("DEVOPS_PASSWORD")),
                "developer" => (Environment.GetEnvironmentVariable("DEVELOPER_USERNAME"), Environment.GetEnvironmentVariable("DEVELOPER_PASSWORD")),
                "tester" => (Environment.GetEnvironmentVariable("TESTER_USERNAME"), Environment.GetEnvironmentVariable("TESTER_PASSWORD")),
                _ => (Environment.GetEnvironmentVariable("DEVELOPER_USERNAME"), Environment.GetEnvironmentVariable("DEVELOPER_PASSWORD")) // Default to developer credentials
            };
        }
    }
}