using Microsoft.AspNetCore.SignalR;
using StreamSync.Services.Interfaces;
using StreamSync.DTOs;
using StreamSync.Hubs;

namespace StreamSync.Services
{
    /// <summary>
    /// Handles all virtual browser-related SignalR notifications.
    /// This service is responsible only for delivering notifications to clients,
    /// keeping notification logic separate from business logic.
    /// </summary>
    public class VirtualBrowserNotificationService : IVirtualBrowserNotificationService
    {
        private readonly IHubContext<RoomHub, IRoomClient> _hubContext;
        private readonly IRoomStateService _roomStateService;
        private readonly ILogger<VirtualBrowserNotificationService> _logger;

        public VirtualBrowserNotificationService(
            IHubContext<RoomHub, IRoomClient> hubContext,
            IRoomStateService roomStateService,
            ILogger<VirtualBrowserNotificationService> logger)
        {
            _hubContext = hubContext;
            _roomStateService = roomStateService;
            _logger = logger;
        }

        public async Task NotifyBrowserAllocatedAsync(string roomId, VirtualBrowserDto browser)
        {
            if (string.IsNullOrEmpty(roomId) || browser == null)
            {
                _logger.LogWarning("Cannot notify browser allocated: roomId or browser is null");
                return;
            }

            _logger.LogInformation("Notifying room {RoomId} that virtual browser {BrowserId} has been allocated", 
                roomId, browser.Id);
            
            await _hubContext.Clients.Group(roomId).VirtualBrowserAllocated(browser);
        }

        public async Task NotifyBrowserReleasedAsync(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                _logger.LogWarning("Cannot notify browser released: roomId is null");
                return;
            }

            _logger.LogInformation("Notifying room {RoomId} that virtual browser has been released", roomId);
            await _hubContext.Clients.Group(roomId).VirtualBrowserReleased();
        }

        public async Task NotifyBrowserExpiredAsync(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                _logger.LogWarning("Cannot notify browser expired: roomId is null");
                return;
            }

            _logger.LogInformation("Notifying room {RoomId} that virtual browser session has expired", roomId);
            await _hubContext.Clients.Group(roomId).VirtualBrowserExpired();
        }

        public async Task NotifyQueuedAsync(string roomId, VirtualBrowserQueueDto queueStatus)
        {
            if (string.IsNullOrEmpty(roomId) || queueStatus == null)
            {
                _logger.LogWarning("Cannot notify queued: roomId or queueStatus is null");
                return;
            }

            _logger.LogInformation("Notifying room {RoomId} about queue position {Position}", 
                roomId, queueStatus.Position);
            
            await _hubContext.Clients.Group(roomId).VirtualBrowserQueued(queueStatus);
        }

        public async Task NotifyQueueCancelledAsync(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                _logger.LogWarning("Cannot notify queue cancelled: roomId is null");
                return;
            }

            _logger.LogInformation("Notifying room {RoomId} that queue request was cancelled", roomId);
            await _hubContext.Clients.Group(roomId).VirtualBrowserQueueCancelled();
        }

        public async Task NotifyBrowserAvailableAsync(string roomId, VirtualBrowserQueueDto queueStatus)
        {
            if (string.IsNullOrEmpty(roomId) || queueStatus == null)
            {
                _logger.LogWarning("Cannot notify browser available: roomId or queueStatus is null");
                return;
            }

            // Prefer notifying only the controller if one exists
            var controller = await _roomStateService.GetControllerAsync(roomId);
            if (controller != null)
            {
                _logger.LogInformation(
                    "Notifying room controller {ControllerId} in room {RoomId} about available virtual browser (Position: {Position})", 
                    controller.Id, roomId, queueStatus.Position);
                
                await _hubContext.Clients.Client(controller.ConnectionId).VirtualBrowserAvailable(queueStatus);
            }
            else
            {
                _logger.LogWarning(
                    "No controller found for room {RoomId}, sending notification to entire room group", roomId);
                
                await _hubContext.Clients.Group(roomId).VirtualBrowserAvailable(queueStatus);
            }
        }

        public async Task NotifyQueueNotificationExpiredAsync(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                _logger.LogWarning("Cannot notify queue notification expired: roomId is null");
                return;
            }

            _logger.LogInformation("Notifying room {RoomId} that queue notification has expired", roomId);
            await _hubContext.Clients.Group(roomId).VirtualBrowserQueueNotificationExpired();
        }

        public async Task NotifyVideoChangedAsync(string roomId, string videoUrl, string videoTitle, string? videoThumbnail)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                _logger.LogWarning("Cannot notify video changed: roomId is null");
                return;
            }

            _logger.LogInformation("Notifying room {RoomId} about video change to: {Title}", roomId, videoTitle);
            await _hubContext.Clients.Group(roomId).VideoChanged(videoUrl, videoTitle, videoThumbnail);
        }
    }
}
