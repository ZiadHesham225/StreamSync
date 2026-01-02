using Microsoft.AspNetCore.SignalR;
using StreamSync.Services.Interfaces;
using StreamSync.Services.InMemory;
using StreamSync.DTOs;
using StreamSync.Hubs;
using StreamSync.Models.InMemory;

namespace StreamSync.Services
{
    /// <summary>
    /// Handles chat message management and notifications.
    /// Keeps chat logic separate from hub implementation.
    /// </summary>
    public class ChatService : IChatService
    {
        private readonly InMemoryRoomManager _roomManager;
        private readonly IHubContext<RoomHub, IRoomClient> _hubContext;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            InMemoryRoomManager roomManager,
            IHubContext<RoomHub, IRoomClient> hubContext,
            ILogger<ChatService> logger)
        {
            _roomManager = roomManager;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<bool> SendMessageAsync(string roomId, string senderId, string senderName, string? avatarUrl, string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Attempted to send empty message in room {RoomId}", roomId);
                    return false;
                }

                var chatMessage = new ChatMessage(senderId, senderName, avatarUrl, content);
                _roomManager.AddMessage(roomId, chatMessage);

                await _hubContext.Clients.Group(roomId).ReceiveMessage(
                    senderId,
                    senderName,
                    avatarUrl,
                    content,
                    DateTime.UtcNow,
                    false);

                _logger.LogDebug("Message sent in room {RoomId} by {SenderName}", roomId, senderName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message in room {RoomId}", roomId);
                return false;
            }
        }

        public async Task SendSystemMessageAsync(string roomId, string content)
        {
            try
            {
                var chatMessage = new ChatMessage("system", "System", null, content);
                _roomManager.AddMessage(roomId, chatMessage);

                await _hubContext.Clients.Group(roomId).ReceiveMessage(
                    "system",
                    "System",
                    null,
                    content,
                    DateTime.UtcNow,
                    true);

                _logger.LogDebug("System message sent in room {RoomId}: {Content}", roomId, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system message in room {RoomId}", roomId);
            }
        }

        public Task<List<ChatMessageDto>> GetChatHistoryAsync(string roomId)
        {
            var messages = _roomManager.GetRoomMessages(roomId);
            var messageDtos = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.SenderName,
                AvatarUrl = m.AvatarUrl,
                Content = m.Content,
                SentAt = m.SentAt
            }).ToList();

            return Task.FromResult(messageDtos);
        }

        public async Task SendChatHistoryToClientAsync(string connectionId, string roomId)
        {
            try
            {
                var history = await GetChatHistoryAsync(roomId);
                await _hubContext.Clients.Client(connectionId).ReceiveChatHistory(history);
                
                _logger.LogDebug("Sent {Count} chat messages to client {ConnectionId} in room {RoomId}", 
                    history.Count, connectionId, roomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat history to client {ConnectionId} in room {RoomId}", 
                    connectionId, roomId);
            }
        }
    }
}
