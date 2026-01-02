using StreamSync.Validations;

namespace StreamSync.DTOs
{
    [RequirePasswordIfPrivate]
    public class RoomUpdateDto : IPrivateRoomDto
    {
        public required string RoomId { get; set; }
        public string? Name { get; set; }
        public string? VideoUrl { get; set; }
        public bool? IsPrivate { get; set; }
        public string? Password { get; set; }
        public bool? AutoPlay { get; set; }
        public string? SyncMode { get; set; }

        // IPrivateRoomDto implementation - return non-nullable bool
        bool IPrivateRoomDto.IsPrivate => IsPrivate ?? false;
    }
}
