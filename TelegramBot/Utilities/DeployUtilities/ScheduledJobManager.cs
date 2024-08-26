using System.Collections.Concurrent;
using TelegramBot.Commands;

namespace TelegramBot.Utilities
{
    public class ScheduledJobManager
    {
        private static ConcurrentDictionary<string, (DateTime ScheduledTime, string JobUrl)> scheduledJobs = new ConcurrentDictionary<string, (DateTime, string)>();
        private static Timer timer;

        public static void Initialize()
        {
            timer = new Timer(CheckScheduledJobs, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        public static string ScheduleJob(string jobUrl, DateTime scheduledTime)
        {
            string jobId = Guid.NewGuid().ToString();
            scheduledJobs[jobId] = (scheduledTime, jobUrl);
            return jobId;
        }

        private static async void CheckScheduledJobs(object state)
        {
            var now = DateTime.Now;
            foreach (var job in scheduledJobs)
            {
                if (job.Value.ScheduledTime <= now)
                {
                    await ExecuteScheduledJob(job.Key, job.Value.JobUrl);
                }
            }
        }

        private static async Task ExecuteScheduledJob(string jobId, string jobUrl)
        {
            var deployResult = await DeployCommand.DeployProjectAsync(jobUrl);
            scheduledJobs.TryRemove(jobId, out _);
        }
    }
}