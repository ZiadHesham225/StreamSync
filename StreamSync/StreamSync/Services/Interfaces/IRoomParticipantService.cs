using StreamSync.DTOs;
using StreamSync.Models.InMemory;

namespace StreamSync.Services.Interfaces
{
    /// <summary>
    /// Service for managing room participants, including mapping to DTOs and notifications.
    /// Centralizes participant-related operations to avoid duplication.
    /// </summary>
    public interface IRoomParticipantService
    {
        /// <summary>
        /// Gets all participants in a room as DTOs.
        /// </summary>
        Task<List<RoomParticipantDto>> GetParticipantDtosAsync(string roomId);

        /// <summary>
        /// Maps a single participant to a DTO.
        /// </summary>
        Task<RoomParticipantDto> MapToDto(string roomId, RoomParticipant participant);

        /// <summary>
        /// Broadcasts the updated participant list to all clients in the room.
        /// </summary>
        Task BroadcastParticipantsAsync(string roomId);

        /// <summary>
        /// Notifies a specific client about the participant list.
        /// </summary>
        Task SendParticipantsToClientAsync(string connectionId, string roomId);

        /// <summary>
        /// Notifies the room when a participant joins.
        /// </summary>
        Task NotifyParticipantJoinedAsync(string roomId, string participantId, string displayName, string avatarUrl);

        /// <summary>
        /// Notifies the room when a participant leaves.
        /// </summary>
        Task NotifyParticipantLeftAsync(string roomId, string participantId, string displayName);

        /// <summary>
        /// Notifies the room when control is transferred to a new participant.
        /// </summary>
        Task NotifyControlTransferredAsync(string roomId, string newControllerId, string newControllerName);
    }
}
