namespace StreamSync.Models.InMemory
{
    public class ChatMessage
    {
        public string Id { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string? AvatarUrl { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }

        public ChatMessage(string senderId, string senderName, string? avatarUrl, string content)
        {
            Id = Guid.NewGuid().ToString();
            SenderId = senderId;
            SenderName = senderName;
            AvatarUrl = avatarUrl;
            Content = content;
            SentAt = DateTime.UtcNow;
        }
    }
}