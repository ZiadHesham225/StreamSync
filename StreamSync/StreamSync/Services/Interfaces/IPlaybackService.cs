using StreamSync.DTOs;

namespace StreamSync.Services.Interfaces
{
    /// <summary>
    /// Service for managing playback synchronization in rooms.
    /// Handles playback state updates and sync notifications.
    /// </summary>
    public interface IPlaybackService
    {
        /// <summary>
        /// Updates the playback state for a room and broadcasts to all participants.
        /// </summary>
        Task<bool> UpdatePlaybackAsync(string roomId, string userId, double position, bool isPlaying);

        /// <summary>
        /// Broadcasts a heartbeat with the current position to all participants except the sender.
        /// </summary>
        Task BroadcastHeartbeatAsync(string roomId, string senderConnectionId, double position);

        /// <summary>
        /// Forces all clients in a room to sync to a specific position.
        /// </summary>
        Task ForceSyncAsync(string roomId, double position, bool isPlaying);

        /// <summary>
        /// Sends the current playback state to a specific client.
        /// </summary>
        Task SendPlaybackStateToClientAsync(string connectionId, string roomId);

        /// <summary>
        /// Notifies all clients in a room about a video change.
        /// </summary>
        Task NotifyVideoChangedAsync(string roomId, string videoUrl, string videoTitle, string? videoThumbnail);
    }
}
