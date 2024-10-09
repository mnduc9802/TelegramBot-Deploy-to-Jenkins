namespace TelegramBot.Utilities.DeployUtilities
{
    public static class JobUrl
    {
        public static (string FolderPath, string JobName) ParseJobUrl(string jobUrl)
        {
            var folderPath = Path.GetDirectoryName(jobUrl)?.Replace("job/", "");
            var jobName = Path.GetFileName(jobUrl);
            return (folderPath, jobName);
        }

        public static string GetFullJobUrl(string folderPath, string jobName)
        {
            return Path.Combine(folderPath.Replace("/", "job/"), jobName);
        }
    }
}