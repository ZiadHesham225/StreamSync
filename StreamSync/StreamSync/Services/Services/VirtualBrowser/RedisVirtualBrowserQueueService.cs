using StreamSync.DataAccess.Interfaces;
using StreamSync.DTOs;
using StreamSync.Services.Interfaces;

namespace StreamSync.Services.Redis
{
    /// <summary>
    /// Redis-based implementation of IVirtualBrowserQueueService.
    /// Ensures queue fairness across all server instances in a horizontally scaled environment.
    /// Uses Redis Sorted Sets for position tracking and Hashes for entry/notification data.
    /// </summary>
    public class RedisVirtualBrowserQueueService : IVirtualBrowserQueueService
    {
        private readonly ICacheService _cache;
        private readonly ILogger<RedisVirtualBrowserQueueService> _logger;

        private const int NOTIFICATION_TIMEOUT_MINUTES = 2;
        private static readonly TimeSpan QueueExpiry = TimeSpan.FromHours(24);

        // Redis key patterns
        private const string QUEUE_ENTRIES_KEY = "vbqueue:entries";           // Hash: roomId -> QueueEntry JSON
        private const string QUEUE_ORDER_KEY = "vbqueue:order";               // Sorted Set: roomId with score = timestamp
        private const string NOTIFICATIONS_KEY = "vbqueue:notifications";      // Hash: roomId -> NotificationEntry JSON

        public RedisVirtualBrowserQueueService(
            ICacheService cache,
            ILogger<RedisVirtualBrowserQueueService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<int> AddToQueueAsync(string roomId)
        {
            try
            {
                // Check if already in queue
                var existingEntry = await _cache.HashGetAsync<QueueEntry>(QUEUE_ENTRIES_KEY, roomId);
                if (existingEntry != null)
                {
                    _logger.LogWarning("Room {RoomId} is already in queue at position {Position}", roomId, existingEntry.Position);
                    return existingEntry.Position;
                }

                var now = DateTime.UtcNow;
                var position = await CalculatePositionAsync();

                var entry = new QueueEntry
                {
                    RoomId = roomId,
                    RequestedAt = now,
                    Position = position,
                    Status = QueueStatus.Waiting
                };

                // Store entry in hash
                await _cache.HashSetAsync(QUEUE_ENTRIES_KEY, roomId, entry, QueueExpiry);

                // Add to sorted set for ordering (score = Unix timestamp for FIFO)
                await _cache.SetAddAsync(QUEUE_ORDER_KEY, $"{now.Ticks}:{roomId}");
                await _cache.SetExpirationAsync(QUEUE_ORDER_KEY, QueueExpiry);

                // Recalculate all positions
                await UpdateQueuePositionsAsync();

                _logger.LogInformation("Added room {RoomId} to queue at position {Position}", roomId, position);
                return position;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding room {RoomId} to queue", roomId);
                throw;
            }
        }

        public async Task<string?> GetNextInQueueAsync()
        {
            try
            {
                var entries = await _cache.HashGetAllAsync<QueueEntry>(QUEUE_ENTRIES_KEY);
                
                // Find the first waiting entry by position
                var nextEntry = entries.Values
                    .Where(e => e.Status == QueueStatus.Waiting)
                    .OrderBy(e => e.Position)
                    .FirstOrDefault();

                if (nextEntry != null)
                {
                    _logger.LogInformation("Retrieved next room from queue: {RoomId} (Position: {Position})", 
                        nextEntry.RoomId, nextEntry.Position);
                    return nextEntry.RoomId;
                }

                _logger.LogDebug("No rooms available in waiting queue");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next room from queue");
                return null;
            }
        }

        public async Task<bool> RemoveFromQueueAsync(string roomId)
        {
            try
            {
                var entry = await _cache.HashGetAsync<QueueEntry>(QUEUE_ENTRIES_KEY, roomId);
                if (entry == null)
                {
                    return false;
                }

                // Remove from entries hash
                await _cache.HashRemoveAsync(QUEUE_ENTRIES_KEY, roomId);

                // Remove from notifications if exists
                await _cache.HashRemoveAsync(NOTIFICATIONS_KEY, roomId);

                // Remove from order set (need to find and remove the entry)
                await RemoveFromOrderSetAsync(roomId);

                // Recalculate positions
                await UpdateQueuePositionsAsync();

                _logger.LogInformation("Removed room {RoomId} from queue", roomId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing room {RoomId} from queue", roomId);
                return false;
            }
        }

        public async Task<VirtualBrowserQueueDto?> GetQueueStatusAsync(string roomId)
        {
            try
            {
                var entry = await _cache.HashGetAsync<QueueEntry>(QUEUE_ENTRIES_KEY, roomId);
                if (entry == null)
                {
                    return null;
                }

                var notification = await _cache.HashGetAsync<NotificationEntry>(NOTIFICATIONS_KEY, roomId);

                return new VirtualBrowserQueueDto
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
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting queue status for room {RoomId}", roomId);
                return null;
            }
        }

        public async Task<List<VirtualBrowserQueueDto>> GetAllQueueStatusAsync()
        {
            try
            {
                var entries = await _cache.HashGetAllAsync<QueueEntry>(QUEUE_ENTRIES_KEY);
                var notifications = await _cache.HashGetAllAsync<NotificationEntry>(NOTIFICATIONS_KEY);

                var result = new List<VirtualBrowserQueueDto>();

                foreach (var kvp in entries)
                {
                    var entry = kvp.Value;
                    notifications.TryGetValue(kvp.Key, out var notification);

                    result.Add(new VirtualBrowserQueueDto
                    {
                        Id = entry.RoomId,
                        RoomId = entry.RoomId,
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

                return result.OrderBy(q => q.Position).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all queue statuses");
                return new List<VirtualBrowserQueueDto>();
            }
        }

        public async Task<bool> NotifyRoomAsync(string roomId)
        {
            try
            {
                var entry = await _cache.HashGetAsync<QueueEntry>(QUEUE_ENTRIES_KEY, roomId);
                if (entry == null || entry.Status != QueueStatus.Waiting)
                {
                    _logger.LogWarning("Failed to notify room {RoomId} - room not found or not in waiting status", roomId);
                    return false;
                }

                // Check if already notified
                var existingNotification = await _cache.HashGetAsync<NotificationEntry>(NOTIFICATIONS_KEY, roomId);
                if (existingNotification != null)
                {
                    _logger.LogWarning("Room {RoomId} is already notified, skipping", roomId);
                    return false;
                }

                // Update entry status
                entry.Status = QueueStatus.Notified;
                await _cache.HashSetAsync(QUEUE_ENTRIES_KEY, roomId, entry, QueueExpiry);

                // Create notification
                var notification = new NotificationEntry
                {
                    RoomId = roomId,
                    NotifiedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(NOTIFICATION_TIMEOUT_MINUTES)
                };
                await _cache.HashSetAsync(NOTIFICATIONS_KEY, roomId, notification, QueueExpiry);

                _logger.LogInformation("Successfully notified room {RoomId} about available virtual browser (Position: {Position})",
                    roomId, entry.Position);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying room {RoomId}", roomId);
                return false;
            }
        }

        public async Task<bool> AcceptNotificationAsync(string roomId)
        {
            try
            {
                var entry = await _cache.HashGetAsync<QueueEntry>(QUEUE_ENTRIES_KEY, roomId);
                if (entry == null || entry.Status != QueueStatus.Notified)
                {
                    return false;
                }

                // Remove from queue completely
                await _cache.HashRemoveAsync(QUEUE_ENTRIES_KEY, roomId);
                await _cache.HashRemoveAsync(NOTIFICATIONS_KEY, roomId);
                await RemoveFromOrderSetAsync(roomId);

                // Recalculate positions
                await UpdateQueuePositionsAsync();

                _logger.LogInformation("Room {RoomId} accepted queue notification", roomId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting notification for room {RoomId}", roomId);
                return false;
            }
        }

        public async Task<bool> DeclineNotificationAsync(string roomId)
        {
            try
            {
                var entry = await _cache.HashGetAsync<QueueEntry>(QUEUE_ENTRIES_KEY, roomId);
                if (entry == null || entry.Status != QueueStatus.Notified)
                {
                    return false;
                }

                // Remove from queue completely (declined = out of queue)
                await _cache.HashRemoveAsync(QUEUE_ENTRIES_KEY, roomId);
                await _cache.HashRemoveAsync(NOTIFICATIONS_KEY, roomId);
                await RemoveFromOrderSetAsync(roomId);

                // Recalculate positions
                await UpdateQueuePositionsAsync();

                _logger.LogInformation("Room {RoomId} declined queue notification and was removed from queue", roomId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining notification for room {RoomId}", roomId);
                return false;
            }
        }

        public async Task<bool> ProcessExpiredNotificationsAsync()
        {
            try
            {
                var notifications = await _cache.HashGetAllAsync<NotificationEntry>(NOTIFICATIONS_KEY);
                var now = DateTime.UtcNow;

                var expiredNotifications = notifications.Values
                    .Where(n => n.ExpiresAt <= now)
                    .ToList();

                foreach (var notification in expiredNotifications)
                {
                    _logger.LogInformation("Queue notification expired for room {RoomId}", notification.RoomId);

                    // Get the entry and reset to waiting
                    var entry = await _cache.HashGetAsync<QueueEntry>(QUEUE_ENTRIES_KEY, notification.RoomId);
                    if (entry != null)
                    {
                        entry.Status = QueueStatus.Waiting;
                        await _cache.HashSetAsync(QUEUE_ENTRIES_KEY, notification.RoomId, entry, QueueExpiry);
                    }

                    // Remove the notification
                    await _cache.HashRemoveAsync(NOTIFICATIONS_KEY, notification.RoomId);
                }

                if (expiredNotifications.Any())
                {
                    await UpdateQueuePositionsAsync();
                    _logger.LogInformation("Processed {Count} expired notifications", expiredNotifications.Count);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired notifications");
                return false;
            }
        }

        public int GetQueueLength()
        {
            try
            {
                var entries = _cache.HashGetAllAsync<QueueEntry>(QUEUE_ENTRIES_KEY).GetAwaiter().GetResult();
                return entries.Count(kvp =>
                    kvp.Value.Status == QueueStatus.Waiting ||
                    kvp.Value.Status == QueueStatus.Notified);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting queue length");
                return 0;
            }
        }

        #region Private Helpers

        private async Task<int> CalculatePositionAsync()
        {
            var entries = await _cache.HashGetAllAsync<QueueEntry>(QUEUE_ENTRIES_KEY);
            var waitingCount = entries.Values.Count(e => e.Status == QueueStatus.Waiting);
            return waitingCount + 1;
        }

        private async Task UpdateQueuePositionsAsync()
        {
            try
            {
                var entries = await _cache.HashGetAllAsync<QueueEntry>(QUEUE_ENTRIES_KEY);

                var waitingEntries = entries.Values
                    .Where(e => e.Status == QueueStatus.Waiting)
                    .OrderBy(e => e.RequestedAt)
                    .ToList();

                for (int i = 0; i < waitingEntries.Count; i++)
                {
                    var entry = waitingEntries[i];
                    if (entry.Position != i + 1)
                    {
                        entry.Position = i + 1;
                        await _cache.HashSetAsync(QUEUE_ENTRIES_KEY, entry.RoomId, entry, QueueExpiry);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating queue positions");
            }
        }

        private async Task RemoveFromOrderSetAsync(string roomId)
        {
            try
            {
                // Get all members and find the one with this roomId
                var members = await _cache.SetMembersAsync(QUEUE_ORDER_KEY);
                var memberToRemove = members.FirstOrDefault(m => m.EndsWith($":{roomId}"));
                if (memberToRemove != null)
                {
                    await _cache.SetRemoveAsync(QUEUE_ORDER_KEY, memberToRemove);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing room {RoomId} from order set", roomId);
            }
        }

        #endregion

        #region Internal Classes

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

        #endregion
    }
}
