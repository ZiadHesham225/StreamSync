using StackExchange.Redis;
using StreamSync.Models.InMemory;
using StreamSync.Services.Interfaces;
using System.Text.Json;

namespace StreamSync.Services.Redis
{
    /// <summary>
    /// Redis-based implementation of IRoomStateService.
    /// Stores room participants and chat messages in Redis for persistence and scalability.
    /// </summary>
    public class RedisRoomStateService : IRoomStateService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly ILogger<RedisRoomStateService> _logger;
        private const int MAX_MESSAGES_PER_ROOM = 50;
        private static readonly TimeSpan RoomExpiry = TimeSpan.FromHours(24);
        private static readonly TimeSpan EmptyRoomExpiry = TimeSpan.FromHours(3); // Keep data 3 hours after room empties

        // Redis key prefixes
        private const string PARTICIPANTS_KEY = "room:{0}:participants";
        private const string MESSAGES_KEY = "room:{0}:messages";
        private const string ACTIVE_ROOMS_KEY = "active_rooms";

        public RedisRoomStateService(IConnectionMultiplexer redis, ILogger<RedisRoomStateService> logger)
        {
            _redis = redis;
            _db = redis.GetDatabase();
            _logger = logger;
        }

        #region Participant Management

        public async Task AddParticipantAsync(string roomId, RoomParticipant participant)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            var json = JsonSerializer.Serialize(participant);
            
            await _db.HashSetAsync(key, participant.Id, json);
            await _db.KeyExpireAsync(key, RoomExpiry);
            
            // Also reset message expiry when someone joins
            var messagesKey = string.Format(MESSAGES_KEY, roomId);
            if (await _db.KeyExistsAsync(messagesKey))
            {
                await _db.KeyExpireAsync(messagesKey, RoomExpiry);
            }
            
            // Track active rooms
            await _db.SetAddAsync(ACTIVE_ROOMS_KEY, roomId);
            
            _logger.LogDebug("Added participant {ParticipantId} to room {RoomId}", participant.Id, roomId);
        }

        public async Task RemoveParticipantAsync(string roomId, string participantId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            await _db.HashDeleteAsync(key, participantId);
            
            // Check if room is now empty
            var count = await _db.HashLengthAsync(key);
            if (count == 0)
            {
                // Room is empty - set messages to expire in 3 hours instead of deleting immediately
                await _db.SetRemoveAsync(ACTIVE_ROOMS_KEY, roomId);
                
                // Set expiry on messages and participants keys (keeps data for 3 hours)
                var messagesKey = string.Format(MESSAGES_KEY, roomId);
                await _db.KeyExpireAsync(key, EmptyRoomExpiry);
                await _db.KeyExpireAsync(messagesKey, EmptyRoomExpiry);
                
                _logger.LogInformation("Room {RoomId} is now empty. Data will expire in {Hours} hours.", roomId, EmptyRoomExpiry.TotalHours);
            }
            
            _logger.LogDebug("Removed participant {ParticipantId} from room {RoomId}", participantId, roomId);
        }

        public async Task<RoomParticipant?> GetParticipantAsync(string roomId, string participantId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            var json = await _db.HashGetAsync(key, participantId);
            
            if (json.IsNullOrEmpty)
                return null;
                
            return JsonSerializer.Deserialize<RoomParticipant>(json!);
        }

        public async Task<List<RoomParticipant>> GetRoomParticipantsAsync(string roomId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            var entries = await _db.HashGetAllAsync(key);
            
            return entries
                .Select(e => JsonSerializer.Deserialize<RoomParticipant>(e.Value!))
                .Where(p => p != null)
                .OrderBy(p => p!.JoinedAt)
                .ToList()!;
        }

        public async Task<RoomParticipant?> GetControllerAsync(string roomId)
        {
            var participants = await GetRoomParticipantsAsync(roomId);
            return participants.FirstOrDefault(p => p.HasControl);
        }

        public async Task SetControllerAsync(string roomId, string participantId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            var entries = await _db.HashGetAllAsync(key);
            
            foreach (var entry in entries)
            {
                var participant = JsonSerializer.Deserialize<RoomParticipant>(entry.Value!);
                if (participant != null)
                {
                    participant.HasControl = participant.Id == participantId;
                    await _db.HashSetAsync(key, participant.Id, JsonSerializer.Serialize(participant));
                }
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
                await _db.HashSetAsync(key, p.Id, JsonSerializer.Serialize(p));
            }

            // Give control to next participant
            if (remaining.Any())
            {
                var next = remaining.First();
                next.HasControl = true;
                await _db.HashSetAsync(key, next.Id, JsonSerializer.Serialize(next));
                
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
                await _db.HashSetAsync(key, oldest.Id, JsonSerializer.Serialize(oldest));
                
                _logger.LogDebug("Assigned control to oldest participant {ParticipantId} in room {RoomId}", oldest.Id, roomId);
            }
            else if (withControl.Count > 1)
            {
                // Multiple have control - keep only the oldest
                var sorted = withControl.OrderBy(p => p.JoinedAt).ToList();
                for (int i = 1; i < sorted.Count; i++)
                {
                    sorted[i].HasControl = false;
                    await _db.HashSetAsync(key, sorted[i].Id, JsonSerializer.Serialize(sorted[i]));
                }
                
                _logger.LogDebug("Fixed multiple controllers in room {RoomId}, kept {ParticipantId}", roomId, sorted[0].Id);
            }
        }

        public async Task<int> GetParticipantCountAsync(string roomId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            return (int)await _db.HashLengthAsync(key);
        }

        public async Task<bool> IsParticipantInRoomAsync(string roomId, string participantId)
        {
            var key = string.Format(PARTICIPANTS_KEY, roomId);
            return await _db.HashExistsAsync(key, participantId);
        }

        public async Task UpdateParticipantConnectionIdAsync(string roomId, string participantId, string newConnectionId)
        {
            var participant = await GetParticipantAsync(roomId, participantId);
            if (participant != null)
            {
                participant.ConnectionId = newConnectionId;
                var key = string.Format(PARTICIPANTS_KEY, roomId);
                await _db.HashSetAsync(key, participantId, JsonSerializer.Serialize(participant));
                
                _logger.LogDebug("Updated connection ID for participant {ParticipantId} in room {RoomId}", participantId, roomId);
            }
        }

        #endregion

        #region Chat Management

        public async Task AddMessageAsync(string roomId, ChatMessage message)
        {
            var key = string.Format(MESSAGES_KEY, roomId);
            var json = JsonSerializer.Serialize(message);
            
            // Add to list and trim to max size
            await _db.ListRightPushAsync(key, json);
            await _db.ListTrimAsync(key, -MAX_MESSAGES_PER_ROOM, -1);
            await _db.KeyExpireAsync(key, RoomExpiry);
            
            _logger.LogDebug("Added message to room {RoomId}", roomId);
        }

        public async Task<List<ChatMessage>> GetRoomMessagesAsync(string roomId)
        {
            var key = string.Format(MESSAGES_KEY, roomId);
            var values = await _db.ListRangeAsync(key, 0, -1);
            
            return values
                .Select(v => JsonSerializer.Deserialize<ChatMessage>(v!))
                .Where(m => m != null)
                .ToList()!;
        }

        #endregion

        #region Room Cleanup

        public async Task ClearRoomDataAsync(string roomId)
        {
            var participantsKey = string.Format(PARTICIPANTS_KEY, roomId);
            var messagesKey = string.Format(MESSAGES_KEY, roomId);
            
            await _db.KeyDeleteAsync(new RedisKey[] { participantsKey, messagesKey });
            await _db.SetRemoveAsync(ACTIVE_ROOMS_KEY, roomId);
            
            _logger.LogDebug("Cleared all data for room {RoomId}", roomId);
        }

        public async Task<List<string>> GetActiveRoomIdsAsync()
        {
            var members = await _db.SetMembersAsync(ACTIVE_ROOMS_KEY);
            return members.Select(m => m.ToString()).ToList();
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
