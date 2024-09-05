namespace TelegramBot.Models
{
    public class ScheduledJob
    {
        public string JobName { get; set; }
        public long UserId { get; set; }
        public DateTime ScheduledTime { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}