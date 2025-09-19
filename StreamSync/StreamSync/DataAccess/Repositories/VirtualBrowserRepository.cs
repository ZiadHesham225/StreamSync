using Microsoft.EntityFrameworkCore;
using StreamSync.Data;
using StreamSync.DataAccess.Interfaces;
using StreamSync.Models;

namespace StreamSync.DataAccess.Repositories
{
    public class VirtualBrowserRepository : GenericRepository<VirtualBrowser>, IVirtualBrowserRepository
    {
        public VirtualBrowserRepository(StreamSyncDbContext context) : base(context)
        {
        }

        public async Task<VirtualBrowser?> GetByRoomIdAsync(string roomId)
        {
            return await _context.VirtualBrowsers
                .Include(vb => vb.Room)
                .FirstOrDefaultAsync(vb => vb.RoomId == roomId && 
                    (vb.Status == VirtualBrowserStatus.Allocated || vb.Status == VirtualBrowserStatus.InUse));
        }

        public async Task<List<VirtualBrowser>> GetAvailableBrowsersAsync()
        {
            return await _context.VirtualBrowsers
                .Where(vb => vb.Status == VirtualBrowserStatus.Available)
                .OrderBy(vb => vb.ContainerIndex)
                .ToListAsync();
        }

        public async Task<VirtualBrowser?> GetByContainerIndexAsync(int containerIndex)
        {
            return await _context.VirtualBrowsers
                .FirstOrDefaultAsync(vb => vb.ContainerIndex == containerIndex &&
                    vb.Status != VirtualBrowserStatus.Deallocated &&
                    vb.Status != VirtualBrowserStatus.Expired);
        }

        public async Task<List<VirtualBrowser>> GetExpiredBrowsersAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.VirtualBrowsers
                .Where(vb => vb.ExpiresAt <= now && 
                    (vb.Status == VirtualBrowserStatus.Allocated || vb.Status == VirtualBrowserStatus.InUse))
                .ToListAsync();
        }

        public async Task<List<VirtualBrowser>> GetExpiredAsync()
        {
            return await GetExpiredBrowsersAsync();
        }

        public async Task<int> GetActiveCountAsync()
        {
            return await _context.VirtualBrowsers
                .CountAsync(vb => vb.Status == VirtualBrowserStatus.Allocated || 
                    vb.Status == VirtualBrowserStatus.InUse);
        }

        public async Task<bool> IsContainerIndexInUseAsync(int containerIndex)
        {
            return await _context.VirtualBrowsers
                .AnyAsync(vb => vb.ContainerIndex == containerIndex && 
                    (vb.Status == VirtualBrowserStatus.Allocated || vb.Status == VirtualBrowserStatus.InUse));
        }
    }
}
