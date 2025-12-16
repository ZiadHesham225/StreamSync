using Microsoft.EntityFrameworkCore;
using StreamSync.Data;
using StreamSync.DataAccess.Interfaces;
using StreamSync.Models;

namespace StreamSync.DataAccess.Repositories
{
    public class RoomRepository : GenericRepository<Room>, IRoomRepository
    {
        public RoomRepository(StreamSyncDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Room>> GetActiveRoomsAsync()
        {
            return await dbSet
                .Where(r => r.IsActive)
                .Include(r => r.Admin)
                .ToListAsync();
        }

        public async Task<IEnumerable<Room>> GetRoomsByAdminAsync(string adminId)
        {
            return await dbSet
                .Where(r => r.AdminId == adminId)
                .Include(r => r.Admin)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Room>> GetActiveRoomsByAdminAsync(string adminId)
        {
            return await dbSet
                .Where(r => r.AdminId == adminId && r.IsActive)
                .Include(r => r.Admin)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<Room> GetRoomByInviteCodeAsync(string inviteCode)
        {
            return await dbSet
                .Include(r => r.Admin)
                .FirstOrDefaultAsync(r => r.InviteCode == inviteCode && r.IsActive);
        }

        public async Task<bool> RoomExistsAsync(string roomId)
        {
            return await dbSet.AnyAsync(r => r.Id == roomId);
        }
    }
}
