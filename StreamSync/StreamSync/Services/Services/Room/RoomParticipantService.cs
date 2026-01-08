using Microsoft.AspNetCore.SignalR;
using StreamSync.Services.Interfaces;
using StreamSync.DTOs;
using StreamSync.Hubs;
using StreamSync.Models.InMemory;

namespace StreamSync.Services
{
    /// <summary>
    /// Centralizes participant management and notifications.
    /// Reduces duplication of participant DTO mapping logic across the codebase.
    /// </summary>
    public class RoomParticipantService : IRoomParticipantService
    {
        private readonly IRoomStateService _roomStateService;
        private readonly IRoomService _roomService;
        private readonly IHubContext<RoomHub, IRoomClient> _hubContext;
        private readonly ILogger<RoomParticipantService> _logger;

        public RoomParticipantService(
            IRoomStateService roomStateService,
            IRoomService roomService,
            IHubContext<RoomHub, IRoomClient> hubContext,
            ILogger<RoomParticipantService> logger)
        {
            _roomStateService = roomStateService;
            _roomService = roomService;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<List<RoomParticipantDto>> GetParticipantDtosAsync(string roomId)
        {
            var participants = await _roomStateService.GetRoomParticipantsAsync(roomId);
            var room = await _roomService.GetRoomByIdAsync(roomId);
            var adminId = room?.AdminId;

            return participants.Select(p => CreateDto(p, adminId)).ToList();
        }

        public async Task<RoomParticipantDto> MapToDto(string roomId, RoomParticipant participant)
        {
            var room = await _roomService.GetRoomByIdAsync(roomId);
            return CreateDto(participant, room?.AdminId);
        }

        public async Task BroadcastParticipantsAsync(string roomId)
        {
            try
            {
                var participantDtos = await GetParticipantDtosAsync(roomId);
                await _hubContext.Clients.Group(roomId).ReceiveRoomParticipants(participantDtos);
                
                _logger.LogDebug("Broadcast {Count} participants to room {RoomId}", 
                    participantDtos.Count, roomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting participants to room {RoomId}", roomId);
            }
        }

        public async Task SendParticipantsToClientAsync(string connectionId, string roomId)
        {
            try
            {
                var participantDtos = await GetParticipantDtosAsync(roomId);
                await _hubContext.Clients.Client(connectionId).ReceiveRoomParticipants(participantDtos);
                
                _logger.LogDebug("Sent {Count} participants to client {ConnectionId} in room {RoomId}", 
                    participantDtos.Count, connectionId, roomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending participants to client {ConnectionId} in room {RoomId}", 
                    connectionId, roomId);
            }
        }

        public async Task NotifyParticipantJoinedAsync(string roomId, string participantId, string displayName, string avatarUrl)
        {
            try
            {
                await _hubContext.Clients.Group(roomId).RoomJoined(roomId, participantId, displayName, avatarUrl);
                await _hubContext.Clients.Group(roomId).ParticipantJoinedNotification(displayName);
                
                _logger.LogDebug("Notified room {RoomId} that {DisplayName} joined", roomId, displayName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying room {RoomId} about participant join", roomId);
            }
        }

        public async Task NotifyParticipantLeftAsync(string roomId, string participantId, string displayName)
        {
            try
            {
                await _hubContext.Clients.Group(roomId).RoomLeft(roomId, participantId, displayName);
                await _hubContext.Clients.Group(roomId).ParticipantLeftNotification(displayName);
                
                _logger.LogDebug("Notified room {RoomId} that {DisplayName} left", roomId, displayName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying room {RoomId} about participant leave", roomId);
            }
        }

        public async Task NotifyControlTransferredAsync(string roomId, string newControllerId, string newControllerName)
        {
            try
            {
                await _hubContext.Clients.Group(roomId).ControlTransferred(newControllerId, newControllerName);
                
                _logger.LogInformation("Notified room {RoomId} about control transfer to {ControllerName}", 
                    roomId, newControllerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying room {RoomId} about control transfer", roomId);
            }
        }

        private static RoomParticipantDto CreateDto(RoomParticipant participant, string? adminId)
        {
            return new RoomParticipantDto
            {
                Id = participant.Id,
                DisplayName = participant.DisplayName,
                AvatarUrl = participant.AvatarUrl,
                HasControl = participant.HasControl,
                JoinedAt = participant.JoinedAt,
                IsAdmin = !string.IsNullOrEmpty(adminId) && participant.Id == adminId
            };
        }
    }
}
