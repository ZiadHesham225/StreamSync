using StreamSync.Models;

namespace StreamSync.DataAccess.Interfaces
{
    public interface IVirtualBrowserRepository : IGenericRepository<VirtualBrowser>
    {
        Task<VirtualBrowser?> GetByRoomIdAsync(string roomId);
        Task<List<VirtualBrowser>> GetAvailableBrowsersAsync();
        Task<VirtualBrowser?> GetByContainerIndexAsync(int containerIndex);
        Task<List<VirtualBrowser>> GetExpiredBrowsersAsync();
        Task<List<VirtualBrowser>> GetExpiredAsync();
        Task<int> GetActiveCountAsync();
        Task<bool> IsContainerIndexInUseAsync(int containerIndex);
    }
}
