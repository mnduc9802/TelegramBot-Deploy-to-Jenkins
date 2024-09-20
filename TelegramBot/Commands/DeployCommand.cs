using System.Data;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.DbContext;
using TelegramBot.Models;
using TelegramBot.Utilities;
using TelegramBot.Utilities.DeployUtilities;

namespace TelegramBot.Commands
{
    public class DeployCommand
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

        public static async Task ExecuteAsync(ITelegramBotClient botClient, Message message, string projectPath, CancellationToken cancellationToken)
        {
            var userId = message.From.Id;
            var userRole = await GetUserRoleAsync(userId);

            var initialMessage = await botClient.SendTextMessageAsync(
                message.Chat.Id,
                $"Đang chuẩn bị triển khai {projectPath}...",
                cancellationToken: cancellationToken
            );

            var jobs = await GetDeployableJobsAsync(projectPath, userRole);

            if (jobs.Count == 0)
            {
                await botClient.DeleteMessageAsync(message.Chat.Id, initialMessage.MessageId, cancellationToken);
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    $"Không tìm thấy job có thể triển khai trong {projectPath}.",
                    cancellationToken: cancellationToken
                );
            }
            else if (jobs.Count == 1)
            {
                await botClient.DeleteMessageAsync(message.Chat.Id, initialMessage.MessageId, cancellationToken);
                var jobUrlId = await GetOrCreateJobUrlId(jobs[0].Url, userId);
                await DeployConfirmation.JobDeployConfirmationKeyboard(
                    botClient,
                    message.Chat.Id,
                    jobUrlId.ToString(),
                    jobs[0].Name.EndsWith("_parameter"),
                    cancellationToken
                );
            }
            else
            {
                await botClient.DeleteMessageAsync(message.Chat.Id, initialMessage.MessageId, cancellationToken);
                JobPaginator.chatState[message.Chat.Id] = (jobs, projectPath);
                await JobPaginator.ShowJobsPage(
                    botClient,
                    message.Chat.Id,
                    jobs,
                    0,
                    projectPath,
                    cancellationToken
                );
            }
        }

        public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var userId = callbackQuery.From.Id;
            var userRole = await GetUserRoleAsync(userId);

            if (callbackQuery.Data.StartsWith("deploy_"))
            {
                var shortId = callbackQuery.Data.Replace("deploy_", "");
                var jobUrl = JobKeyboardManager.GetJobUrl(shortId);
                if (jobUrl != null)
                {
                    var jobUrlId = await GetOrCreateJobUrlId(jobUrl, userId);
                    bool hasParameter = jobUrl.EndsWith("_parameter");
                    await DeployConfirmation.JobDeployConfirmationKeyboard(
                        botClient,
                        chatId,
                        jobUrlId.ToString(),
                        hasParameter,
                        cancellationToken,
                        callbackQuery.Message.MessageId
                    );
                }
                else
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Không tìm thấy thông tin job", cancellationToken: cancellationToken);
                }
            }
            else if (callbackQuery.Data.StartsWith("confirm_job_yes_"))
            {
                var jobUrl = callbackQuery.Data.Replace("confirm_job_yes_", "");
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
                var deployResult = await DeployProjectAsync(jobUrl, userRole);
                await SendDeployResultAsync(botClient, chatId, jobUrl, deployResult, cancellationToken);
            }
            else if (callbackQuery.Data == "confirm_job_no")
            {
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
                await botClient.SendTextMessageAsync(chatId, "Yêu cầu triển khai job đã bị hủy.", cancellationToken: cancellationToken);
            }
            else
            {
                await JobPaginator.HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            }
        }

        private static async Task<List<JobInfo>> GetDeployableJobsAsync(string folderPath, string userRole)
        {
            string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
            using var client = new HttpClient { BaseAddress = new Uri(jenkinsUrl) };
            var credentials = GetCredentialsForRole(userRole);
            var byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return await GetJobsRecursivelyAsync(client, folderPath, folderPath);
        }

        private static async Task<List<JobInfo>> GetJobsRecursivelyAsync(HttpClient client, string currentPath, string rootPath)
        {
            var result = new List<JobInfo>();

            var response = await client.GetAsync($"/job/{currentPath.Replace("/", "/job/")}/api/json?tree=jobs[name,url,color]");

            if (!response.IsSuccessStatusCode) return result;

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            foreach (var job in json["jobs"])
            {
                var jobName = job["name"].ToString();
                var jobUrl = job["url"].ToString();
                string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
                var relativeUrl = jobUrl.Replace(jenkinsUrl + "/job/", "").TrimEnd('/');

                if (job["color"] != null)
                {
                    result.Add(new JobInfo { Name = jobName, Url = relativeUrl });
                }
                else
                {
                    var subFolderPath = currentPath + "/" + jobName;
                    result.AddRange(await GetJobsRecursivelyAsync(client, subFolderPath, rootPath));
                }
            }

            return result;
        }

        public static async Task<bool> DeployProjectAsync(string projectPath, string userRole, string parameter = null)
        {
            try
            {
                string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
                using var client = new HttpClient { BaseAddress = new Uri(jenkinsUrl) };
                var credentials = EnvironmentVariableLoader.GetCredentialsForRole(userRole);
                var byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                var crumbResponse = await client.GetAsync("/crumbIssuer/api/json");
                if (!crumbResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to get crumb. Status code: {crumbResponse.StatusCode}");
                    return false;
                }

                var crumbJson = JObject.Parse(await crumbResponse.Content.ReadAsStringAsync());
                client.DefaultRequestHeaders.Add(crumbJson["crumbRequestField"].ToString(), crumbJson["crumb"].ToString());

                var url = $"/job/{projectPath.Replace("/", "/")}/build";
                if (!string.IsNullOrEmpty(parameter))
                {
                    url += $"WithParameters?VERSION={parameter}";
                }
                Console.WriteLine($"Sending request to: {client.BaseAddress}{url}");

                var response = await client.PostAsync(url, null);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Deployment failed. Status code: {response.StatusCode}");
                    Console.WriteLine($"Response content: {await response.Content.ReadAsStringAsync()}");
                }
                else
                {
                    Console.WriteLine("Deployment request sent successfully.");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deployment failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<string> GetUserRoleAsync(long userId)
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT role FROM user_roles WHERE user_id = @userId";
            var parameters = new Dictionary<string, object> { { "@userId", userId } };
            var result = await dbConnection.ExecuteScalarAsync(sql, parameters);
            return result?.ToString() ?? "unknown";
        }

        private static (string Username, string Password) GetCredentialsForRole(string role)
        {
            return EnvironmentVariableLoader.GetCredentialsForRole(role);
        }

        private static async Task<List<ScheduledJob>> GetScheduledJobsAsync()
        {
            var dbConnection = new DatabaseConnection(Program.connectionString);
            var sql = "SELECT job_name, scheduled_time, created_at FROM jobs WHERE scheduled_time IS NOT NULL ORDER BY scheduled_time";
            var dataTable = await dbConnection.ExecuteReaderAsync(sql);

            var scheduledJobs = new List<ScheduledJob>();
            foreach (DataRow row in dataTable.Rows)
            {
                scheduledJobs.Add(new ScheduledJob
                {
                    JobName = row["job_name"].ToString(),
                    ScheduledTime = Convert.ToDateTime(row["scheduled_time"]),
                    CreatedAt = Convert.ToDateTime(row["created_at"])
                });
            }

            return scheduledJobs;
        }


        public static async Task HandleScheduleJobAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var jobUrlId = callbackQuery.Data.Replace("schedule_job_", "");
            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);

            await RequestScheduleTimeAsync(botClient, callbackQuery.Message.Chat.Id, jobUrlId, cancellationToken);
        }


        private static async Task RequestScheduleTimeAsync(ITelegramBotClient botClient, long chatId, string jobUrlId, CancellationToken cancellationToken)
        {
            var scheduledJobs = await GetScheduledJobsAsync();

            string jobList = scheduledJobs.Any()
                ? string.Join("\n", scheduledJobs.Select((j, index) => $"{index + 1}. {j.JobName} - {j.ScheduledTime:dd/MM/yyyy HH:mm}"))
                : "Chưa có job nào được lên lịch.";

            var message = $"*Các job đang được lên lịch:*\n\n{jobList}\n\nVui lòng nhập thời gian bạn muốn lên lịch triển khai job (định dạng: DD/MM/YYYY HH:mm)";

            await botClient.SendTextMessageAsync(
                chatId,
                message,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: cancellationToken);

            Program.schedulingState[chatId] = $"schedule_time_{jobUrlId}";
        }

        public static async Task HandleScheduleTimeInputAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            string messageText = message.Text.Trim();

            if (messageText.ToLower().Contains("hủy") || messageText.ToLower().Contains("cancel"))
            {
                Program.schedulingState.TryRemove(message.Chat.Id, out _);
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Lệnh lên lịch đã bị hủy. Vui lòng /deploy để triển khai lại.",
                    cancellationToken: cancellationToken);
                return;
            }

            DateTime scheduledTime;

            if (messageText.ToLower().Contains("df"))
            {
                scheduledTime = DateTime.Now.AddMinutes(30);
            }
            else if (DateTime.TryParseExact(messageText, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out scheduledTime))
            {
                if (scheduledTime <= DateTime.Now)
                {
                    await botClient.SendTextMessageAsync(
                        message.Chat.Id,
                        "Thời gian lên lịch phải là trong tương lai. Vui lòng thử lại.",
                        cancellationToken: cancellationToken);
                    return;
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Định dạng thời gian không hợp lệ. Vui lòng nhập lại theo định dạng DD/MM/YYYY HH:mm hoặc nhập 'df' để đặt lịch sau 30 phút.",
                    cancellationToken: cancellationToken);
                return;
            }

            string[] parts = Program.schedulingState[message.Chat.Id].Split('_');
            string jobUrlId = parts[2];

            var jobUrl = await GetJobUrlFromId(int.Parse(jobUrlId));
            bool hasParameter = jobUrl.EndsWith("_parameter");

            if (hasParameter)
            {
                await RequestScheduleVersionAsync(botClient, message.Chat.Id, jobUrlId, scheduledTime, cancellationToken);
            }
            else
            {
                await HandleScheduleInputAsync(botClient, message, jobUrl, scheduledTime, null, cancellationToken);
            }
        }
        private static async Task RequestScheduleVersionAsync(ITelegramBotClient botClient, long chatId, string jobUrlId, DateTime scheduledTime, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "Vui lòng nhập tham số VERSION cho job được lên lịch:",
                cancellationToken: cancellationToken);

            Program.schedulingState[chatId] = $"schedule_version_{jobUrlId}|{scheduledTime:yyyy-MM-dd HH:mm:ss}";
        }


        public static async Task HandleScheduleParameterInputAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            string parameter = message.Text.Trim();
            string[] jobInfo = Program.schedulingState[message.Chat.Id].Split('|');
            string jobUrl = jobInfo[0];
            DateTime scheduledTime = DateTime.Parse(jobInfo[1]);

            await HandleScheduleInputAsync(botClient, message, jobUrl, scheduledTime, parameter, cancellationToken);
        }

        private static async Task HandleScheduleInputAsync(ITelegramBotClient botClient, Message message, string jobUrl, DateTime scheduledTime, string parameter, CancellationToken cancellationToken)
        {
            string jobName = jobUrl.TrimEnd('/');
            var dbConnection = new DatabaseConnection(Program.connectionString);
            string sql;
            Dictionary<string, object> parameters;

            sql = "INSERT INTO jobs (job_name, url, scheduled_time, created_at, user_id, parameter) VALUES (@jobName, @url, @scheduledTime, @createdAt, @userId, @parameter)";
            parameters = new Dictionary<string, object>
            {
                { "@jobName", jobName },
                { "@url", jobUrl },
                { "@scheduledTime", scheduledTime },
                { "@createdAt", DateTime.Now },
                { "@userId", message.From.Id },
                { "@parameter", parameter ?? (object)DBNull.Value }
            };

            await dbConnection.ExecuteNonQueryAsync(sql, parameters);

            string confirmationMessage = parameter != null
                ? $"Đã lên lịch triển khai job {jobName} vào lúc {scheduledTime:dd/MM/yyyy HH:mm} với tham số VERSION: {parameter}."
                : $"Đã lên lịch triển khai job {jobName} vào lúc {scheduledTime:dd/MM/yyyy HH:mm}.";

            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                confirmationMessage,
                cancellationToken: cancellationToken);

            Program.schedulingState.TryRemove(message.Chat.Id, out _);
        }



        private static string NormalizeJenkinsPath(string projectPath)
        {
            return string.Join("/job/", projectPath.Split(new[] { "job/" }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static async Task SendDeployResultAsync(ITelegramBotClient botClient, long chatId, string project, bool success, CancellationToken cancellationToken)
        {
            var resultMessage = success ? $"Triển khai {project} thành công!" : $"Triển khai {project} thất bại.";
            await botClient.SendTextMessageAsync(chatId, resultMessage, cancellationToken: cancellationToken);
        }
    }
}