using Microsoft.AspNetCore.SignalR;
using StreamSync.Services.Interfaces;
using StreamSync.Hubs;

namespace StreamSync.Services
{
    /// <summary>
    /// Handles playback synchronization and state management.
    /// Separates playback concerns from the RoomHub.
    /// </summary>
    public class PlaybackService : IPlaybackService
    {
        private readonly IRoomService _roomService;
        private readonly IHubContext<RoomHub, IRoomClient> _hubContext;
        private readonly ILogger<PlaybackService> _logger;

        public PlaybackService(
            IRoomService roomService,
            IHubContext<RoomHub, IRoomClient> hubContext,
            ILogger<PlaybackService> logger)
        {
            _roomService = roomService;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<bool> UpdatePlaybackAsync(string roomId, string userId, double position, bool isPlaying)
        {
            try
            {
                var success = await _roomService.UpdatePlaybackStateAsync(roomId, userId, position, isPlaying);
                if (success)
                {
                    await _hubContext.Clients.Group(roomId).ReceivePlaybackUpdate(position, isPlaying);
                    _logger.LogDebug("Playback updated in room {RoomId}: pos={Position}, playing={IsPlaying}", 
                        roomId, position, isPlaying);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating playback in room {RoomId}", roomId);
                return false;
            }
        }

        public async Task BroadcastHeartbeatAsync(string roomId, string senderConnectionId, double position)
        {
            try
            {
                await _hubContext.Clients.GroupExcept(roomId, senderConnectionId).ReceiveHeartbeat(position);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting heartbeat in room {RoomId}", roomId);
            }
        }

        public async Task ForceSyncAsync(string roomId, double position, bool isPlaying)
        {
            try
            {
                await _hubContext.Clients.Group(roomId).ForceSyncPlayback(position, isPlaying);
                _logger.LogDebug("Forced sync in room {RoomId}: pos={Position}, playing={IsPlaying}", 
                    roomId, position, isPlaying);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forcing sync in room {RoomId}", roomId);
            }
        }

        public async Task SendPlaybackStateToClientAsync(string connectionId, string roomId)
        {
            try
            {
                var room = await _roomService.GetRoomByIdAsync(roomId);
                if (room != null && room.IsActive)
                {
                    await _hubContext.Clients.Client(connectionId).ForceSyncPlayback(room.CurrentPosition, room.IsPlaying);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending playback state to client {ConnectionId} in room {RoomId}", 
                    connectionId, roomId);
            }
        }

        public async Task NotifyVideoChangedAsync(string roomId, string videoUrl, string videoTitle, string? videoThumbnail)
        {
            try
            {
                await _hubContext.Clients.Group(roomId).VideoChanged(videoUrl, videoTitle, videoThumbnail);
                _logger.LogInformation("Video changed in room {RoomId}: {Title}", roomId, videoTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying video change in room {RoomId}", roomId);
            }
        }
    }
}
