using StreamSync.Validations;

namespace StreamSync.DTOs
{
    [RequirePasswordIfPrivate]
    public class RoomCreateDto : IPrivateRoomDto
    {
        public required string Name { get; set; }
        public string? VideoUrl { get; set; } // Made optional for rooms without initial video
        public bool IsPrivate { get; set; } = false;
        public string? Password { get; set; }
        public bool AutoPlay { get; set; } = true;
        public required string SyncMode { get; set; } = "strict";
    }
}
