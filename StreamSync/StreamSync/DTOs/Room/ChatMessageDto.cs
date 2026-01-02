namespace StreamSync.DTOs
{
    public class ChatMessageDto
    {
        public required string Id { get; set; }
        public required string SenderId { get; set; }
        public required string SenderName { get; set; }
        public string? AvatarUrl { get; set; }
        public required string Content { get; set; }
        public DateTime SentAt { get; set; }
    }
}