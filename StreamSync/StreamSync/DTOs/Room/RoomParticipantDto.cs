namespace StreamSync.DTOs
{
    public class RoomParticipantDto
    {
        public required string Id { get; set; }
        public required string DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public bool HasControl { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsAdmin { get; set; }
    }
}