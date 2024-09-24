namespace TelegramBot.Models
{
    public class Job
    {
        public int Id { get; set; }
        public string JobName { get; set; }
        public string Url { get; set; }
        public long UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ScheduledTime { get; set; }
        public string Parameter { get; set; }
    }
}