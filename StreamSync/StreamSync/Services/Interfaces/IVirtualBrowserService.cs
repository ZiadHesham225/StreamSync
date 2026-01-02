using StreamSync.DTOs;
using StreamSync.Models;

namespace StreamSync.Services.Interfaces
{
    public interface IVirtualBrowserService
    {
        Task<bool> InitializePoolAsync();
        Task<VirtualBrowserDto?> RequestVirtualBrowserAsync(string roomId);
        Task<bool> ReleaseVirtualBrowserAsync(string roomId);
        Task<VirtualBrowserDto?> GetRoomVirtualBrowserAsync(string roomId);
        Task<VirtualBrowserDto?> GetVirtualBrowserAsync(string virtualBrowserId);
        Task<VirtualBrowserQueueDto?> GetRoomQueueStatusAsync(string roomId);
        Task<object> GetRoomCooldownStatusAsync(string roomId);
        Task<bool> AcceptQueueNotificationAsync(string roomId);
        Task<bool> DeclineQueueNotificationAsync(string roomId);
        Task<bool> CancelQueueAsync(string roomId);
        Task<List<VirtualBrowserDto>> GetAllVirtualBrowsersAsync();
        Task<List<VirtualBrowserQueueDto>> GetQueueStatusAsync();
        Task ProcessExpiredSessionsAsync();
        Task ProcessQueueNotificationsAsync();
        Task<bool> RestartBrowserProcessAsync(string virtualBrowserId);
    }
}
