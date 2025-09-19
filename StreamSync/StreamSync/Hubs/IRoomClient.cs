using StreamSync.DTOs;

namespace StreamSync.Hubs
{
    public interface IRoomClient
    {
        Task RoomJoined(string roomId, string participantId, string displayName, string avatarUrl);
        Task RoomLeft(string roomId, string participantId, string displayName);
        Task ParticipantJoinedNotification(string displayName);
        Task ParticipantLeftNotification(string displayName);
        Task RoomClosed(string roomId, string reason);
        Task VideoChanged(string videoUrl, string videoTitle, string? videoThumbnail);
        Task ReceivePlaybackUpdate(double position, bool isPlaying);
        Task ForceSyncPlayback(double position, bool isPlaying);
        Task ReceiveHeartbeat(double position);
        Task ControlTransferred(string newControllerId, string newControllerName);
        Task ReceiveMessage(string senderId, string senderName, string? avatarUrl, string message, DateTime timestamp, bool isFromGuest);
        Task ReceiveRoomParticipants(IEnumerable<RoomParticipantDto> participants);
        Task ReceiveChatHistory(IEnumerable<ChatMessageDto> messages);
        Task UserKicked(string roomId, string reason);
        Task Error(string message);

        // Virtual Browser methods
        Task VirtualBrowserAllocated(VirtualBrowserDto virtualBrowser);
        Task VirtualBrowserReleased();
        Task VirtualBrowserExpired();
        Task VirtualBrowserQueued(VirtualBrowserQueueDto queueStatus);
        Task VirtualBrowserQueueCancelled();
        Task VirtualBrowserAvailable(VirtualBrowserQueueDto queueStatus);
        Task VirtualBrowserQueueNotificationExpired();
        Task VirtualBrowserNavigated(string url);
        Task VirtualBrowserControlUpdate(VirtualBrowserControlDto control);
        
        // Room settings
        Task SyncModeChanged(string syncMode);
    }
}
