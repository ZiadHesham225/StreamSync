using StreamSync.DTOs;
using StreamSync.Models;

namespace StreamSync.BusinessLogic.Interfaces
{
    public interface IRoomService
    {
        Task<Room> CreateRoomAsync(RoomCreateDto roomDto, string userId);
        Task<Room> GetRoomByIdAsync(string roomId);
        Task<Room> GetRoomByInviteCodeAsync(string inviteCode);
        Task<IEnumerable<RoomDto>> GetActiveRoomsAsync();
        Task<PagedResultDto<RoomDto>> GetActiveRoomsAsync(PaginationQueryDto pagination);
        Task<IEnumerable<RoomDto>> GetUserRoomsAsync(string userId);
        Task<PagedResultDto<RoomDto>> GetUserRoomsAsync(string userId, PaginationQueryDto pagination);
        Task<bool> UpdateRoomAsync(RoomUpdateDto roomDto, string userId);
        Task<bool> UpdateRoomVideoAsync(string roomId, string videoUrl);
        Task<bool> EndRoomAsync(string roomId, string userId);
        Task<bool> UpdatePlaybackStateAsync(string roomId, string userId, double position, bool isPlaying);
        Task<bool> ValidateRoomPasswordAsync(string roomId, string? password);
        Task<string> GenerateInviteLink(string roomId);
        Task<bool> IsUserAdminAsync(string roomId, string userId);
        Task<bool> CanUserControlRoomAsync(string roomId, string userId);
        Task<bool> UpdateSyncModeAsync(string roomId, string syncMode);
        
        // Removed methods that are now handled in-memory:
        // - GetRoomUsersAsync, GetRegisteredRoomUsersAsync (participants are in-memory)
        // - JoinRoomAsync, JoinRoomAsGuestAsync, LeaveRoomAsync, GuestLeaveRoomAsync (handled by RoomHub)
        // - TransferAdminAsync, UpdateUserPermissionsAsync (control is handled in-memory)
        // - IsUserInRoomAsync (participants are in-memory)
    }
}
