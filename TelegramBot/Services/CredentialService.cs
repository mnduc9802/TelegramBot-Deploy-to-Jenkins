using TelegramBot.Data.DbContext;
using TelegramBot.Utilities.EnvironmentUtilities;

namespace TelegramBot.Services
{
    public class CredentialService
    {
        public static async Task<string> GetUserRoleAsync(long userId)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT role FROM user_roles WHERE user_id = @userId";
            var parameters = new Dictionary<string, object> { { "@userId", userId } };
            var result = await dbConnection.ExecuteScalarAsync(sql, parameters);
            return result?.ToString() ?? "unknown";
        }

        public static (string Username, string Password) GetCredentialsForRole(string role)
        {
            return EnvironmentVariableLoader.GetCredentialsForRole(role);
        }
    }
}
