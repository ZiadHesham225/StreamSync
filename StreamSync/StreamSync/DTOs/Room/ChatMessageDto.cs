namespace StreamSync.DTOs
{
    public class ChatMessageDto
    {
        public string Id { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string? AvatarUrl { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
    }
}