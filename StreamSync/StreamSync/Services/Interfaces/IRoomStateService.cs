using StreamSync.Models.InMemory;

namespace StreamSync.Services.Interfaces
{
    /// <summary>
    /// Service for managing room state including participants and chat messages.
    /// This interface abstracts the storage mechanism (in-memory or Redis).
    /// </summary>
    public interface IRoomStateService
    {
        #region Participant Management

        /// <summary>
        /// Adds a participant to a room.
        /// </summary>
        Task AddParticipantAsync(string roomId, RoomParticipant participant);

        /// <summary>
        /// Removes a participant from a room.
        /// </summary>
        Task RemoveParticipantAsync(string roomId, string participantId);

        /// <summary>
        /// Gets a specific participant from a room.
        /// </summary>
        Task<RoomParticipant?> GetParticipantAsync(string roomId, string participantId);

        /// <summary>
        /// Gets all participants in a room.
        /// </summary>
        Task<List<RoomParticipant>> GetRoomParticipantsAsync(string roomId);

        /// <summary>
        /// Gets the current controller (user with control) in a room.
        /// </summary>
        Task<RoomParticipant?> GetControllerAsync(string roomId);

        /// <summary>
        /// Sets a new controller for a room.
        /// </summary>
        Task SetControllerAsync(string roomId, string participantId);

        /// <summary>
        /// Transfers control to the next participant when the current controller leaves.
        /// </summary>
        Task TransferControlToNextAsync(string roomId, string currentControllerId);

        /// <summary>
        /// Ensures exactly one participant has control in a room.
        /// </summary>
        Task EnsureControlConsistencyAsync(string roomId);

        /// <summary>
        /// Gets the count of participants in a room.
        /// </summary>
        Task<int> GetParticipantCountAsync(string roomId);

        /// <summary>
        /// Checks if a participant is in a specific room.
        /// </summary>
        Task<bool> IsParticipantInRoomAsync(string roomId, string participantId);

        /// <summary>
        /// Updates a participant's connection ID (for reconnection scenarios).
        /// </summary>
        Task UpdateParticipantConnectionIdAsync(string roomId, string participantId, string newConnectionId);

        #endregion

        #region Chat Management

        /// <summary>
        /// Adds a message to a room's chat history.
        /// </summary>
        Task AddMessageAsync(string roomId, ChatMessage message);

        /// <summary>
        /// Gets the chat history for a room.
        /// </summary>
        Task<List<ChatMessage>> GetRoomMessagesAsync(string roomId);

        #endregion

        #region Room Cleanup

        /// <summary>
        /// Clears all data for a room.
        /// </summary>
        Task ClearRoomDataAsync(string roomId);

        /// <summary>
        /// Gets all active room IDs.
        /// </summary>
        Task<List<string>> GetActiveRoomIdsAsync();

        /// <summary>
        /// Cleans up empty rooms.
        /// </summary>
        Task CleanupEmptyRoomsAsync();

        #endregion
    }
}
