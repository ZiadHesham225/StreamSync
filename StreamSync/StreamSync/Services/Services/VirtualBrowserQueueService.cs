using System.Collections.Concurrent;
using StreamSync.DTOs;
using StreamSync.BusinessLogic.Interfaces;

namespace StreamSync.BusinessLogic.Services
{
    public class VirtualBrowserQueueService : IVirtualBrowserQueueService
    {
        private readonly ConcurrentQueue<string> _waitingQueue = new();
        private readonly ConcurrentDictionary<string, QueueEntry> _queueEntries = new();
        private readonly ConcurrentDictionary<string, NotificationEntry> _notifications = new();
        private readonly ILogger<VirtualBrowserQueueService> _logger;
        
        private const int NOTIFICATION_TIMEOUT_MINUTES = 2;

        public VirtualBrowserQueueService(ILogger<VirtualBrowserQueueService> logger)
        {
            _logger = logger;
        }

        public async Task<int> AddToQueueAsync(string roomId)
        {
            if (_queueEntries.ContainsKey(roomId))
            {
                _logger.LogWarning("Room {RoomId} is already in queue", roomId);
                return _queueEntries[roomId].Position;
            }

            var currentWaitingCount = _queueEntries.Values.Count(e => e.Status == QueueStatus.Waiting);
            var position = currentWaitingCount + 1;
            
            var entry = new QueueEntry
            {
                RoomId = roomId,
                RequestedAt = DateTime.UtcNow,
                Position = position,
                Status = QueueStatus.Waiting
            };

            _queueEntries[roomId] = entry;
            _waitingQueue.Enqueue(roomId);

            UpdateQueuePositions();

            _logger.LogInformation("Added room {RoomId} to queue at position {Position}", roomId, entry.Position);
            
            return await Task.FromResult(entry.Position);
        }

        public async Task<string?> GetNextInQueueAsync()
        {
            if (_waitingQueue.TryDequeue(out var roomId))
            {
                if (_queueEntries.TryGetValue(roomId, out var entry) && entry.Status == QueueStatus.Waiting)
                {
                    _logger.LogInformation("Retrieved next room from queue: {RoomId} (Position: {Position})", roomId, entry.Position);
                    return await Task.FromResult(roomId);
                }
                else
                {
                    _logger.LogWarning("Room {RoomId} was dequeued but entry was missing or not in waiting status. Status: {Status}", 
                        roomId, entry?.Status.ToString() ?? "Missing");
                    return await GetNextInQueueAsync();
                }
            }

            _logger.LogDebug("No rooms available in waiting queue");
            return await Task.FromResult<string?>(null);
        }

        public async Task<bool> RemoveFromQueueAsync(string roomId)
        {
            if (_queueEntries.TryRemove(roomId, out var entry))
            {
                _notifications.TryRemove(roomId, out _);
                
                UpdateQueuePositions();
                
                _logger.LogInformation("Removed room {RoomId} from queue", roomId);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        public async Task<VirtualBrowserQueueDto?> GetQueueStatusAsync(string roomId)
        {
            if (_queueEntries.TryGetValue(roomId, out var entry))
            {
                var notification = _notifications.TryGetValue(roomId, out var notif) ? notif : null;
                
                return await Task.FromResult(new VirtualBrowserQueueDto
                {
                    Id = roomId,
                    RoomId = roomId,
                    RequestedAt = entry.RequestedAt,
                    Position = entry.Position,
                    Status = entry.Status.ToString(),
                    NotifiedAt = notification?.NotifiedAt,
                    NotificationExpiresAt = notification?.ExpiresAt,
                    NotificationTimeRemaining = notification?.ExpiresAt > DateTime.UtcNow 
                        ? notification.ExpiresAt - DateTime.UtcNow 
                        : TimeSpan.Zero
                });
            }

            return await Task.FromResult<VirtualBrowserQueueDto?>(null);
        }

        public async Task<List<VirtualBrowserQueueDto>> GetAllQueueStatusAsync()
        {
            var result = new List<VirtualBrowserQueueDto>();
            
            foreach (var kvp in _queueEntries)
            {
                var status = await GetQueueStatusAsync(kvp.Key);
                if (status != null)
                {
                    result.Add(status);
                }
            }

            return result.OrderBy(q => q.Position).ToList();
        }

        public async Task<bool> NotifyRoomAsync(string roomId)
        {
            if (_queueEntries.TryGetValue(roomId, out var entry) && entry.Status == QueueStatus.Waiting)
            {
                if (_notifications.ContainsKey(roomId))
                {
                    _logger.LogWarning("Room {RoomId} is already notified, skipping", roomId);
                    return await Task.FromResult(false);
                }

                entry.Status = QueueStatus.Notified;
                
                var notification = new NotificationEntry
                {
                    RoomId = roomId,
                    NotifiedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(NOTIFICATION_TIMEOUT_MINUTES)
                };

                _notifications[roomId] = notification;
                
                _logger.LogInformation("Successfully notified room {RoomId} about available virtual browser (Position: {Position})", 
                    roomId, entry.Position);
                return await Task.FromResult(true);
            }

            _logger.LogWarning("Failed to notify room {RoomId} - room not found or not in waiting status", roomId);
            return await Task.FromResult(false);
        }

        public async Task<bool> AcceptNotificationAsync(string roomId)
        {
            if (_queueEntries.TryGetValue(roomId, out var entry) && entry.Status == QueueStatus.Notified)
            {
                _queueEntries.TryRemove(roomId, out _);
                _notifications.TryRemove(roomId, out _);
                
                UpdateQueuePositions();
                
                _logger.LogInformation("Room {RoomId} accepted queue notification", roomId);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        public async Task<bool> DeclineNotificationAsync(string roomId)
        {
            if (_queueEntries.TryGetValue(roomId, out var entry) && entry.Status == QueueStatus.Notified)
            {
                _queueEntries.TryRemove(roomId, out _);
                _notifications.TryRemove(roomId, out _);
                
                UpdateQueuePositions();
                
                _logger.LogInformation("Room {RoomId} declined queue notification and was removed from queue", roomId);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        public async Task<bool> ProcessExpiredNotificationsAsync()
        {
            var expiredNotifications = _notifications.Values
                .Where(n => n.ExpiresAt <= DateTime.UtcNow)
                .ToList();

            foreach (var notification in expiredNotifications)
            {
                _logger.LogInformation("Queue notification expired for room {RoomId}", notification.RoomId);
                
                if (_queueEntries.TryGetValue(notification.RoomId, out var entry))
                {
                    entry.Status = QueueStatus.Waiting;
                    _waitingQueue.Enqueue(notification.RoomId);
                }
                
                _notifications.TryRemove(notification.RoomId, out _);
            }

            if (expiredNotifications.Any())
            {
                UpdateQueuePositions();
                _logger.LogInformation("Processed {Count} expired notifications", expiredNotifications.Count);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        public int GetQueueLength()
        {
            return _queueEntries.Count(kvp => 
                kvp.Value.Status == QueueStatus.Waiting || 
                kvp.Value.Status == QueueStatus.Notified);
        }

        private void UpdateQueuePositions()
        {
            var waitingEntries = _queueEntries.Values
                .Where(e => e.Status == QueueStatus.Waiting)
                .OrderBy(e => e.RequestedAt)
                .ToList();

            for (int i = 0; i < waitingEntries.Count; i++)
            {
                waitingEntries[i].Position = i + 1;
            }
        }

        private class QueueEntry
        {
            public string RoomId { get; set; } = string.Empty;
            public DateTime RequestedAt { get; set; }
            public int Position { get; set; }
            public QueueStatus Status { get; set; }
        }

        private class NotificationEntry
        {
            public string RoomId { get; set; } = string.Empty;
            public DateTime NotifiedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }

        private enum QueueStatus
        {
            Waiting,
            Notified
        }
    }
}
