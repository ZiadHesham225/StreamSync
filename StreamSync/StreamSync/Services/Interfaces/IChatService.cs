using StreamSync.DTOs;

namespace StreamSync.Services.Interfaces
{
    /// <summary>
    /// Service for managing chat operations in rooms.
    /// Handles message storage, history retrieval, and notifications.
    /// </summary>
    public interface IChatService
    {
        /// <summary>
        /// Sends a message to a room and broadcasts it to all participants.
        /// </summary>
        Task<bool> SendMessageAsync(string roomId, string senderId, string senderName, string? avatarUrl, string content);

        /// <summary>
        /// Sends a system message to a room (e.g., user kicked notifications).
        /// </summary>
        Task SendSystemMessageAsync(string roomId, string content);

        /// <summary>
        /// Gets the chat history for a room.
        /// </summary>
        Task<List<ChatMessageDto>> GetChatHistoryAsync(string roomId);

        /// <summary>
        /// Sends the chat history to a specific client.
        /// </summary>
        Task SendChatHistoryToClientAsync(string connectionId, string roomId);
    }
}
