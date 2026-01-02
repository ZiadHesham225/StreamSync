using StreamSync.DTOs;

namespace StreamSync.Services.Interfaces
{
    /// <summary>
    /// Service responsible for sending virtual browser-related notifications to clients via SignalR.
    /// Follows Single Responsibility Principle - only handles notification delivery.
    /// </summary>
    public interface IVirtualBrowserNotificationService
    {
        /// <summary>
        /// Notifies all clients in a room that a virtual browser has been allocated.
        /// </summary>
        Task NotifyBrowserAllocatedAsync(string roomId, VirtualBrowserDto browser);

        /// <summary>
        /// Notifies all clients in a room that the virtual browser has been released.
        /// </summary>
        Task NotifyBrowserReleasedAsync(string roomId);

        /// <summary>
        /// Notifies all clients in a room that the virtual browser session has expired.
        /// </summary>
        Task NotifyBrowserExpiredAsync(string roomId);

        /// <summary>
        /// Notifies all clients in a room that they have been added to the queue.
        /// </summary>
        Task NotifyQueuedAsync(string roomId, VirtualBrowserQueueDto queueStatus);

        /// <summary>
        /// Notifies all clients in a room that the queue request was cancelled.
        /// </summary>
        Task NotifyQueueCancelledAsync(string roomId);

        /// <summary>
        /// Notifies the room controller (or entire room if no controller) that a browser is available.
        /// </summary>
        Task NotifyBrowserAvailableAsync(string roomId, VirtualBrowserQueueDto queueStatus);

        /// <summary>
        /// Notifies all clients in a room that the queue notification has expired.
        /// </summary>
        Task NotifyQueueNotificationExpiredAsync(string roomId);

        /// <summary>
        /// Notifies all clients in a room that the video has changed (used when switching to virtual browser mode).
        /// </summary>
        Task NotifyVideoChangedAsync(string roomId, string videoUrl, string videoTitle, string? videoThumbnail);
    }
}
