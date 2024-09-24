using TelegramBot.DbContext;
using TelegramBot.Utilities;
using TelegramBot.Utilities.EnvironmentUtilities;

namespace TelegramBot.Commands.MajorCommands.DeployCommand
{
    public class JobExtension
    {
        public static async Task<int> GetOrCreateJobUrlId(string url, long userId, string parameter = null)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();

            // Xử lý URL một cách an toàn hơn
            string path = url;
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                path = uri.AbsolutePath.TrimStart('/');
            }

            if (path.StartsWith("job/", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(4);
            }

            var selectSql = "SELECT id FROM jobs WHERE url = @url";
            var selectParams = new Dictionary<string, object> { { "@url", path } };
            var result = await dbConnection.ExecuteScalarAsync(selectSql, selectParams);

            if (result != null)
            {
                int jobId = Convert.ToInt32(result);
                // Job exists, update the created_at timestamp and parameter
                var updateSql = "UPDATE jobs SET created_at = @created_at, parameter = @parameter WHERE id = @id";
                var updateParams = new Dictionary<string, object>
                {
                    { "@created_at", DateTime.Now },
                    { "@id", jobId },
                    { "@parameter", parameter ?? (object)DBNull.Value }
                };
                await dbConnection.ExecuteNonQueryAsync(updateSql, updateParams);
                return jobId;
            }

            // Job doesn't exist, create a new one
            var insertSql = "INSERT INTO jobs (job_name, url, user_id, created_at, parameter) VALUES (@job_name, @url, @user_id, @created_at, @parameter) RETURNING id";
            var insertParams = new Dictionary<string, object>
            {
                { "@job_name", path },
                { "@url", path },
                { "@user_id", userId },
                { "@created_at", DateTime.Now },
                { "@parameter", parameter ?? (object)DBNull.Value }
            };
            result = await dbConnection.ExecuteScalarAsync(insertSql, insertParams);
            return Convert.ToInt32(result);
        }


        public static async Task<string> GetJobUrlFromId(int id)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT url FROM jobs WHERE id = @id";
            var parameters = new Dictionary<string, object> { { "@id", id } };
            var result = await dbConnection.ExecuteScalarAsync(sql, parameters);

            return result?.ToString();
        }

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