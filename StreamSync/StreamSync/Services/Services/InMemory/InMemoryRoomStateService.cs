using System.Collections.Concurrent;
using StreamSync.Models.RealTime;
using StreamSync.Services.Interfaces;

namespace StreamSync.Services.InMemory
{
    /// <summary>
    /// In-memory implementation of IRoomStateService.
    /// Used as a fallback when Redis is not configured or for development.
    /// </summary>
    public class InMemoryRoomStateService : IRoomStateService
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, RoomParticipant>> _roomParticipants = new();
        private readonly ConcurrentDictionary<string, Queue<ChatMessage>> _roomMessages = new();
        private readonly ConcurrentDictionary<string, DateTime> _emptyRoomTimestamps = new(); // Track when rooms became empty
        private readonly ILogger<InMemoryRoomStateService> _logger;
        private const int MAX_MESSAGES_PER_ROOM = 50;
        private static readonly TimeSpan EmptyRoomRetention = TimeSpan.FromHours(3); // Keep data 3 hours after room empties

        public InMemoryRoomStateService(ILogger<InMemoryRoomStateService> logger)
        {
            _logger = logger;
        }

        #region Participant Management

        public Task AddParticipantAsync(string roomId, RoomParticipant participant)
        {
            var participants = _roomParticipants.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, RoomParticipant>());
            participants[participant.Id] = participant;
            
            // Remove from empty room tracking if someone joins
            _emptyRoomTimestamps.TryRemove(roomId, out _);
            
            return Task.CompletedTask;
        }

        public Task RemoveParticipantAsync(string roomId, string participantId)
        {
            if (_roomParticipants.TryGetValue(roomId, out var participants))
            {
                participants.TryRemove(participantId, out _);
                
                if (participants.IsEmpty)
                {
                    // Mark the room as empty but DON'T delete the data yet
                    _roomParticipants.TryRemove(roomId, out _);
                    _emptyRoomTimestamps[roomId] = DateTime.UtcNow;
                    
                    _logger.LogInformation("Room {RoomId} is now empty. Messages will be retained for {Hours} hours.", 
                        roomId, EmptyRoomRetention.TotalHours);
                }
            }
            return Task.CompletedTask;
        }

        public Task<RoomParticipant?> GetParticipantAsync(string roomId, string participantId)
        {
            var result = _roomParticipants.TryGetValue(roomId, out var participants) && 
                         participants.TryGetValue(participantId, out var participant) 
                ? participant 
                : null;
            return Task.FromResult(result);
        }

        public Task<List<RoomParticipant>> GetRoomParticipantsAsync(string roomId)
        {
            var result = _roomParticipants.TryGetValue(roomId, out var participants) 
                ? participants.Values.OrderBy(p => p.JoinedAt).ToList()
                : new List<RoomParticipant>();
            return Task.FromResult(result);
        }

        public Task<RoomParticipant?> GetControllerAsync(string roomId)
        {
            if (_roomParticipants.TryGetValue(roomId, out var participants))
            {
                return Task.FromResult(participants.Values.FirstOrDefault(p => p.HasControl));
            }
            return Task.FromResult<RoomParticipant?>(null);
        }

        public Task SetControllerAsync(string roomId, string participantId)
        {
            if (_roomParticipants.TryGetValue(roomId, out var participants))
            {
                foreach (var participant in participants.Values)
                {
                    participant.HasControl = participant.Id == participantId;
                }
            }
            return Task.CompletedTask;
        }

        public Task TransferControlToNextAsync(string roomId, string currentControllerId)
        {
            if (!_roomParticipants.TryGetValue(roomId, out var participants))
                return Task.CompletedTask;

            var remaining = participants.Values
                .Where(p => p.Id != currentControllerId)
                .OrderBy(p => p.JoinedAt)
                .ToList();
            
            foreach (var participant in participants.Values)
            {
                participant.HasControl = false;
            }

            if (remaining.Any())
            {
                remaining.First().HasControl = true;
            }
            
            return Task.CompletedTask;
        }

        public Task EnsureControlConsistencyAsync(string roomId)
        {
            if (!_roomParticipants.TryGetValue(roomId, out var participants) || !participants.Any())
                return Task.CompletedTask;

            var withControl = participants.Values.Where(p => p.HasControl).ToList();
            
            if (!withControl.Any())
            {
                var oldest = participants.Values.OrderBy(p => p.JoinedAt).First();
                oldest.HasControl = true;
            }
            else if (withControl.Count > 1)
            {
                var sorted = withControl.OrderBy(p => p.JoinedAt).ToList();
                for (int i = 1; i < sorted.Count; i++)
                {
                    sorted[i].HasControl = false;
                }
            }
            
            return Task.CompletedTask;
        }

        public Task<int> GetParticipantCountAsync(string roomId)
        {
            var count = _roomParticipants.TryGetValue(roomId, out var participants) 
                ? participants.Count 
                : 0;
            return Task.FromResult(count);
        }

        public Task<bool> IsParticipantInRoomAsync(string roomId, string participantId)
        {
            var result = _roomParticipants.TryGetValue(roomId, out var participants) && 
                         participants.ContainsKey(participantId);
            return Task.FromResult(result);
        }

        public Task UpdateParticipantConnectionIdAsync(string roomId, string participantId, string newConnectionId)
        {
            if (_roomParticipants.TryGetValue(roomId, out var participants) &&
                participants.TryGetValue(participantId, out var participant))
            {
                participant.ConnectionId = newConnectionId;
            }
            return Task.CompletedTask;
        }

        #endregion

        #region Chat Management

        public Task AddMessageAsync(string roomId, ChatMessage message)
        {
            var messages = _roomMessages.GetOrAdd(roomId, _ => new Queue<ChatMessage>());
            
            lock (messages)
            {
                messages.Enqueue(message);
                
                while (messages.Count > MAX_MESSAGES_PER_ROOM)
                {
                    messages.Dequeue();
                }
            }
            
            return Task.CompletedTask;
        }

        public Task<List<ChatMessage>> GetRoomMessagesAsync(string roomId)
        {
            if (_roomMessages.TryGetValue(roomId, out var messages))
            {
                lock (messages)
                {
                    return Task.FromResult(messages.ToList());
                }
            }
            return Task.FromResult(new List<ChatMessage>());
        }

        #endregion

        #region Room Cleanup

        public Task ClearRoomDataAsync(string roomId)
        {
            _roomParticipants.TryRemove(roomId, out _);
            _roomMessages.TryRemove(roomId, out _);
            _emptyRoomTimestamps.TryRemove(roomId, out _);
            return Task.CompletedTask;
        }

        public Task<List<string>> GetActiveRoomIdsAsync()
        {
            // Return rooms that have participants OR have messages pending cleanup
            var activeRooms = _roomParticipants.Keys.ToList();
            var roomsWithMessages = _roomMessages.Keys.Except(activeRooms).ToList();
            activeRooms.AddRange(roomsWithMessages);
            return Task.FromResult(activeRooms.Distinct().ToList());
        }

        public Task CleanupEmptyRoomsAsync()
        {
            var now = DateTime.UtcNow;
            var roomsToCleanup = new List<string>();
            
            // Find rooms that have been empty for longer than the retention period
            foreach (var kvp in _emptyRoomTimestamps)
            {
                if (now - kvp.Value > EmptyRoomRetention)
                {
                    roomsToCleanup.Add(kvp.Key);
                }
            }
            
            foreach (var roomId in roomsToCleanup)
            {
                _roomMessages.TryRemove(roomId, out _);
                _emptyRoomTimestamps.TryRemove(roomId, out _);
                _logger.LogInformation("Cleaned up expired room data for {RoomId} after {Hours} hours of inactivity.", 
                    roomId, EmptyRoomRetention.TotalHours);
            }
            
            // Also clean up any empty participant dictionaries that somehow got left behind
            var emptyParticipants = _roomParticipants
                .Where(kvp => kvp.Value.IsEmpty)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var roomId in emptyParticipants)
            {
                _roomParticipants.TryRemove(roomId, out _);
            }
            
            return Task.CompletedTask;
        }

        #endregion
    }
}
