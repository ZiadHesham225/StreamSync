namespace StreamSync.DTOs
{
    public class RoomDto
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string VideoUrl { get; set; }
        public required string AdminId { get; set; }
        public required string AdminName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public required string InviteCode { get; set; }
        public bool IsPrivate { get; set; }
        public bool HasPassword { get; set; }
        public int UserCount { get; set; }
    }
}
