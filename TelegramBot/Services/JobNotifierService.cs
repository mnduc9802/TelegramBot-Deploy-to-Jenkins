using System.Text;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using Telegram.Bot;
using TelegramBot.Utilities.EnvironmentUtilities;
using System.Collections.Concurrent;

namespace TelegramBot.Services
{
    public class JobNotifierService
    {
        private static readonly ConcurrentDictionary<string, JobNotificationInfo> _activeJobs = new();
        private static readonly HttpClient _httpClient;
        private static Timer _pollTimer;
        private const int POLLING_INTERVAL = 10000; // 10 seconds

        static JobNotifierService()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(EnvironmentVariableLoader.GetJenkinsUrl()) };
            //var credentials = EnvironmentVariableLoader.GetCredentialsForRole("admin");
            //var byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}");
            //_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            _pollTimer = new Timer(PollJobs, null, POLLING_INTERVAL, POLLING_INTERVAL);
        }

        public class JobNotificationInfo
        {
            public string JobUrl { get; set; }
            public long ChatId { get; set; }
            public int BuildNumber { get; set; }
            public NotificationPreference Preference { get; set; }
            public bool IsScheduled { get; set; }
            public DateTime? ScheduledTime { get; set; }
        }

        public enum NotificationPreference
        {
            All,
            FailureOnly,
            None
        }

        public static void AddJobToNotify(string jobUrl, long chatId, NotificationPreference preference, bool isScheduled = false, DateTime? scheduledTime = null)
        {
            _activeJobs.TryAdd(jobUrl, new JobNotificationInfo
            {
                JobUrl = jobUrl,
                ChatId = chatId,
                BuildNumber = -1,
                Preference = preference,
                IsScheduled = isScheduled,
                ScheduledTime = scheduledTime
            });
        }

        private static async void PollJobs(object state)
        {
            foreach (var jobInfo in _activeJobs.Values)
            {
                try
                {
                    var buildInfo = await GetLatestBuildInfo(jobInfo.JobUrl);
                    if (buildInfo == null) continue;

                    int currentBuildNumber = buildInfo.Value.buildNumber;
                    bool isBuilding = buildInfo.Value.isBuilding;
                    string result = buildInfo.Value.result;

                    if (jobInfo.BuildNumber == -1)
                    {
                        jobInfo.BuildNumber = currentBuildNumber;
                        continue;
                    }

                    if (currentBuildNumber > jobInfo.BuildNumber && !isBuilding)
                    {
                        // Build completed
                        await NotifyUser(jobInfo, result);
                        _activeJobs.TryRemove(jobInfo.JobUrl, out _);
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogError(ex, $"Error polling job {jobInfo.JobUrl}");
                }
            }
        }

        private static async Task<(int buildNumber, bool isBuilding, string result)?> GetLatestBuildInfo(string jobUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/job/{jobUrl.Replace("/", "/job/")}/lastBuild/api/json");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                return (
                    json["number"]?.Value<int>() ?? -1,
                    json["building"]?.Value<bool>() ?? false,
                    json["result"]?.ToString() ?? "UNKNOWN"
                );
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, $"Error getting build info for {jobUrl}");
                return null;
            }
        }

        private static async Task NotifyUser(JobNotificationInfo jobInfo, string buildResult)
        {
            if (jobInfo.Preference == NotificationPreference.None) return;
            if (jobInfo.Preference == NotificationPreference.FailureOnly && buildResult != "FAILURE") return;

            string message = jobInfo.IsScheduled
                ? $"Job đã lên lịch của bạn {jobInfo.JobUrl} đã hoàn thành với kết quả: {buildResult}"
                : $"Job {jobInfo.JobUrl} đã hoàn thành với kết quả: {buildResult}";

            try
            {
                await Program.botClient.SendTextMessageAsync(jobInfo.ChatId, message);
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, $"Error sending notification for {jobInfo.JobUrl}");
            }
        }
    }
}