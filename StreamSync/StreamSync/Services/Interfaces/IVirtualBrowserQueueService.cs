using StreamSync.DTOs;

namespace StreamSync.BusinessLogic.Interfaces
{
    public interface IVirtualBrowserQueueService
    {
        Task<int> AddToQueueAsync(string roomId);
        Task<string?> GetNextInQueueAsync();
        Task<bool> RemoveFromQueueAsync(string roomId);
        Task<VirtualBrowserQueueDto?> GetQueueStatusAsync(string roomId);
        Task<List<VirtualBrowserQueueDto>> GetAllQueueStatusAsync();
        Task<bool> NotifyRoomAsync(string roomId);
        Task<bool> AcceptNotificationAsync(string roomId);
        Task<bool> DeclineNotificationAsync(string roomId);
        Task<bool> ProcessExpiredNotificationsAsync();
        int GetQueueLength();
    }
}