using StreamSync.DataAccess.Interfaces;
using StreamSync.Models.RealTime;
using StreamSync.Services.Interfaces;

namespace StreamSync.Services.Redis
{
    /// <summary>
    /// Redis-based implementation of IRoomStateService.
    /// Stores room participants and chat messages in Redis for persistence and scalability.
    /// Uses ICacheService abstraction to avoid direct Redis dependency in service layer.
    /// </summary>
    public class RedisRoomStateService : IRoomStateService
    {
        private readonly ICacheService _cache;
        private readonly ILogger<RedisRoomStateService> _logger;
        private const int MAX_MESSAGES_PER_ROOM = 50;
        private static readonly TimeSpan RoomExpiry = TimeSpan.FromHours(24);
        private static readonly TimeSpan EmptyRoomExpiry = TimeSpan.FromHours(3);

        // Cache key formats
        private const string PARTICIPANTS_KEY = "roomstate:{0}:participants";
        private const string MESSAGES_KEY = "roomstate:{0}:messages";
        private const string ACTIVE_ROOMS_KEY = "roomstate:active_rooms";

        public RedisRoomStateService(ICacheService cache, ILogger<RedisRoomStateService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        #region Participant Management

        public async Task AddParticipantAsync(string roomId, RoomParticipant participant)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            
            await _cache.HashSetAsync(key, participant.Id, participant, RoomExpiry);
            
            // Also reset message expiry when someone joins
            var messagesKey = string.Format(MESSAGES_KEY, roomId);
            if (await _cache.ExistsAsync(messagesKey))
            {
                await _cache.SetExpirationAsync(messagesKey, RoomExpiry);
            }
            
            // Track active rooms
            await _cache.SetAddAsync(ACTIVE_ROOMS_KEY, roomId);
            
            _logger.LogDebug("Added participant {ParticipantId} to room {RoomId}", participant.Id, roomId);
        }

        public async Task RemoveParticipantAsync(string roomId, string participantId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            await _cache.HashRemoveAsync(key, participantId);
            
            // Check if room is now empty
            var count = await _cache.HashLengthAsync(key);
            if (count == 0)
            {
                // Room is empty - set messages to expire in 3 hours
                await _cache.SetRemoveAsync(ACTIVE_ROOMS_KEY, roomId);
                
                // Set expiry on messages and participants keys
                var messagesKey = string.Format(MESSAGES_KEY, roomId);
                await _cache.SetExpirationAsync(key, EmptyRoomExpiry);
                await _cache.SetExpirationAsync(messagesKey, EmptyRoomExpiry);
                
                _logger.LogInformation("Room {RoomId} is now empty. Data will expire in {Hours} hours.", roomId, EmptyRoomExpiry.TotalHours);
            }
            
            _logger.LogDebug("Removed participant {ParticipantId} from room {RoomId}", participantId, roomId);
        }

        public async Task<RoomParticipant?> GetParticipantAsync(string roomId, string participantId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            return await _cache.HashGetAsync<RoomParticipant>(key, participantId);
        }

        public async Task<List<RoomParticipant>> GetRoomParticipantsAsync(string roomId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            var entries = await _cache.HashGetAllAsync<RoomParticipant>(key);
            
            return entries.Values
                .OrderBy(p => p.JoinedAt)
                .ToList();
        }

        public async Task<RoomParticipant?> GetControllerAsync(string roomId)
        {
            var participants = await GetRoomParticipantsAsync(roomId);
            return participants.FirstOrDefault(p => p.HasControl);
        }

        public async Task SetControllerAsync(string roomId, string participantId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            var entries = await _cache.HashGetAllAsync<RoomParticipant>(key);
            
            foreach (var entry in entries)
            {
                var participant = entry.Value;
                participant.HasControl = participant.Id == participantId;
                await _cache.HashSetAsync(key, participant.Id, participant);
            }
            
            _logger.LogDebug("Set controller to {ParticipantId} in room {RoomId}", participantId, roomId);
        }

        public async Task TransferControlToNextAsync(string roomId, string currentControllerId)
        {
            var participants = await GetRoomParticipantsAsync(roomId);
            var remaining = participants
                .Where(p => p.Id != currentControllerId)
                .OrderBy(p => p.JoinedAt)
                .ToList();

            var key = string.Format(PARTICIPANTS_KEY, roomId);
            
            // Remove control from all
            foreach (var p in participants)
            {
                p.HasControl = false;
                await _cache.HashSetAsync(key, p.Id, p);
            }

            // Give control to next participant
            if (remaining.Any())
            {
                var next = remaining.First();
                next.HasControl = true;
                await _cache.HashSetAsync(key, next.Id, next);
                
                _logger.LogDebug("Transferred control to {ParticipantId} in room {RoomId}", next.Id, roomId);
            }
        }

        public async Task EnsureControlConsistencyAsync(string roomId)
        {
            var participants = await GetRoomParticipantsAsync(roomId);
            if (!participants.Any())
                return;

            var key = string.Format(PARTICIPANTS_KEY, roomId);
            var withControl = participants.Where(p => p.HasControl).ToList();

            if (!withControl.Any())
            {
                // No one has control - give to oldest participant
                var oldest = participants.OrderBy(p => p.JoinedAt).First();
                oldest.HasControl = true;
                await _cache.HashSetAsync(key, oldest.Id, oldest);
                
                _logger.LogDebug("Assigned control to oldest participant {ParticipantId} in room {RoomId}", oldest.Id, roomId);
            }
            else if (withControl.Count > 1)
            {
                // Multiple have control - keep only the oldest
                var sorted = withControl.OrderBy(p => p.JoinedAt).ToList();
                for (int i = 1; i < sorted.Count; i++)
                {
                    sorted[i].HasControl = false;
                    await _cache.HashSetAsync(key, sorted[i].Id, sorted[i]);
                }
                
                _logger.LogDebug("Fixed multiple controllers in room {RoomId}, kept {ParticipantId}", roomId, sorted[0].Id);
            }
        }

        public async Task<int> GetParticipantCountAsync(string roomId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            return (int)await _cache.HashLengthAsync(key);
        }

        public async Task<bool> IsParticipantInRoomAsync(string roomId, string participantId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            return await _cache.HashExistsAsync(key, participantId);
        }

        public async Task UpdateParticipantConnectionIdAsync(string roomId, string participantId, string newConnectionId)
        {
            var participant = await GetParticipantAsync(roomId, participantId);
            if (participant != null)
            {
                participant.ConnectionId = newConnectionId;
                var key = string.Format(PARTICIPANTS_KEY, roomId);
                await _cache.HashSetAsync(key, participantId, participant);
                
                _logger.LogDebug("Updated connection ID for participant {ParticipantId} in room {RoomId}", participantId, roomId);
            }
        }

        #endregion

        #region Chat Management

        public async Task AddMessageAsync(string roomId, ChatMessage message)
        {
            var key = string.Format(MESSAGES_KEY, roomId);
            await _cache.ListPushAsync(key, message, MAX_MESSAGES_PER_ROOM, RoomExpiry);
            
            _logger.LogDebug("Added message to room {RoomId}", roomId);
        }

        public async Task<List<ChatMessage>> GetRoomMessagesAsync(string roomId)
        {
            var key = string.Format(MESSAGES_KEY, roomId);
            return await _cache.ListRangeAsync<ChatMessage>(key);
        }

        #endregion

        #region Room Cleanup

        public async Task ClearRoomDataAsync(string roomId)
        {
            var participantsKey = string.Format(PARTICIPANTS_KEY, roomId);
            var messagesKey = string.Format(MESSAGES_KEY, roomId);
            
            await _cache.RemoveAsync(participantsKey);
            await _cache.RemoveAsync(messagesKey);
            await _cache.SetRemoveAsync(ACTIVE_ROOMS_KEY, roomId);
            
            _logger.LogDebug("Cleared all data for room {RoomId}", roomId);
        }

        public async Task<List<string>> GetActiveRoomIdsAsync()
        {
            var members = await _cache.SetMembersAsync(ACTIVE_ROOMS_KEY);
            return members.ToList();
        }

        public async Task CleanupEmptyRoomsAsync()
        {
            var roomIds = await GetActiveRoomIdsAsync();
            
            foreach (var roomId in roomIds)
            {
                var count = await GetParticipantCountAsync(roomId);
                if (count == 0)
                {
                    await ClearRoomDataAsync(roomId);
                    _logger.LogInformation("Cleaned up empty room {RoomId}", roomId);
                }
            }
        }

        #endregion
    }
}
