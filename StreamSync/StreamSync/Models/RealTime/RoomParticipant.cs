namespace StreamSync.Models.RealTime
{
    public class RoomParticipant
    {
        public string Id { get; set; }
        public string ConnectionId { get; set; }
        public string DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public bool HasControl { get; set; }
        public DateTime JoinedAt { get; set; }
        
        public RoomParticipant(string id, string connectionId, string displayName, string? avatarUrl, bool hasControl = false)
        {
            Id = id;
            ConnectionId = connectionId;
            DisplayName = displayName;
            AvatarUrl = avatarUrl;
            HasControl = hasControl;
            JoinedAt = DateTime.UtcNow;
        }
    }
}
