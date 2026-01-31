namespace StreamSync.DTOs
{
    public class RoomDetailDto : RoomDto
    {
        public double CurrentPosition { get; set; }
        public bool IsPlaying { get; set; }
        public required string SyncMode { get; set; }
        public bool AutoPlay { get; set; }
        public List<RoomParticipantDto> Participants { get; set; } = new List<RoomParticipantDto>();
    }
}
