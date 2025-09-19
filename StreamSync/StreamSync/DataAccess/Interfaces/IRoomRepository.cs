using StreamSync.Models;

namespace StreamSync.DataAccess.Interfaces
{
    public interface IRoomRepository : IGenericRepository<Room>
    {
        Task<IEnumerable<Room>> GetActiveRoomsAsync();
        Task<IEnumerable<Room>> GetRoomsByAdminAsync(string adminId);
        Task<Room> GetRoomByInviteCodeAsync(string inviteCode);
        Task<bool> RoomExistsAsync(string roomId);
    }
}
