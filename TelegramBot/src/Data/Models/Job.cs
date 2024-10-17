namespace TelegramBot.Data.Models
{
    public class Job
    {
        public int Id { get; set; }
        public string JobName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ScheduledTime { get; set; }
        public string? Parameter { get; set; }
    }
}