using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using StreamSync.Services;
using StreamSync.Services.InMemory;
using StreamSync.DTOs;
using StreamSync.Hubs;
using StreamSync.Models.InMemory;

namespace StreamSync.Tests.Services
{
    public class ChatServiceTests
    {
        private readonly Mock<IHubContext<RoomHub, IRoomClient>> _mockHubContext;
        private readonly Mock<IRoomClient> _mockClientProxy;
        private readonly Mock<ILogger<ChatService>> _mockLogger;
        private readonly InMemoryRoomManager _roomManager;
        private readonly ChatService _chatService;

        public ChatServiceTests()
        {
            _mockHubContext = new Mock<IHubContext<RoomHub, IRoomClient>>();
            _mockClientProxy = new Mock<IRoomClient>();
            _mockLogger = new Mock<ILogger<ChatService>>();
            _roomManager = new InMemoryRoomManager();

            var mockClients = new Mock<IHubClients<IRoomClient>>();
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            mockClients.Setup(c => c.Client(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

            _chatService = new ChatService(
                _roomManager,
                _mockHubContext.Object,
                _mockLogger.Object);
        }

        #region SendMessageAsync Tests

        [Fact]
        public async Task SendMessageAsync_WithValidMessage_ShouldBroadcastAndReturnTrue()
        {
            // Arrange
            var roomId = "room-1";
            var senderId = "user-1";
            var senderName = "TestUser";
            var avatarUrl = "https://avatar.com/1.png";
            var content = "Hello, World!";

            // Act
            var result = await _chatService.SendMessageAsync(roomId, senderId, senderName, avatarUrl, content);

            // Assert
            result.Should().BeTrue();
            _mockClientProxy.Verify(
                c => c.ReceiveMessage(senderId, senderName, avatarUrl, content, It.IsAny<DateTime>(), false),
                Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_WithEmptyContent_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-1";
            var senderId = "user-1";
            var senderName = "TestUser";
            var avatarUrl = "https://avatar.com/1.png";
            var content = "";

            // Act
            var result = await _chatService.SendMessageAsync(roomId, senderId, senderName, avatarUrl, content);

            // Assert
            result.Should().BeFalse();
            _mockClientProxy.Verify(
                c => c.ReceiveMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), 
                    It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task SendMessageAsync_WithWhitespaceContent_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-1";
            var senderId = "user-1";
            var senderName = "TestUser";
            var content = "   ";

            // Act
            var result = await _chatService.SendMessageAsync(roomId, senderId, senderName, null, content);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task SendMessageAsync_WithNullAvatarUrl_ShouldStillSucceed()
        {
            // Arrange
            var roomId = "room-1";
            var senderId = "user-1";
            var senderName = "TestUser";
            var content = "Test message";

            // Act
            var result = await _chatService.SendMessageAsync(roomId, senderId, senderName, null, content);

            // Assert
            result.Should().BeTrue();
            _mockClientProxy.Verify(
                c => c.ReceiveMessage(senderId, senderName, null, content, It.IsAny<DateTime>(), false),
                Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_ShouldStoreMessageInRoomManager()
        {
            // Arrange
            var roomId = "room-1";
            var senderId = "user-1";
            var senderName = "TestUser";
            var content = "Stored message";

            // Act
            await _chatService.SendMessageAsync(roomId, senderId, senderName, null, content);

            // Assert
            var messages = _roomManager.GetRoomMessages(roomId);
            messages.Should().HaveCount(1);
            messages.First().Content.Should().Be(content);
            messages.First().SenderId.Should().Be(senderId);
            messages.First().SenderName.Should().Be(senderName);
        }

        #endregion

        #region SendSystemMessageAsync Tests

        [Fact]
        public async Task SendSystemMessageAsync_ShouldBroadcastSystemMessage()
        {
            // Arrange
            var roomId = "room-1";
            var content = "User has been kicked from the room";

            // Act
            await _chatService.SendSystemMessageAsync(roomId, content);

            // Assert
            _mockClientProxy.Verify(
                c => c.ReceiveMessage("system", "System", null, content, It.IsAny<DateTime>(), true),
                Times.Once);
        }

        [Fact]
        public async Task SendSystemMessageAsync_ShouldStoreSystemMessageInRoomManager()
        {
            // Arrange
            var roomId = "room-1";
            var content = "System notification";

            // Act
            await _chatService.SendSystemMessageAsync(roomId, content);

            // Assert
            var messages = _roomManager.GetRoomMessages(roomId);
            messages.Should().HaveCount(1);
            messages.First().SenderId.Should().Be("system");
            messages.First().SenderName.Should().Be("System");
            messages.First().Content.Should().Be(content);
        }

        #endregion

        #region GetChatHistoryAsync Tests

        [Fact]
        public async Task GetChatHistoryAsync_WithMessages_ShouldReturnAllMessages()
        {
            // Arrange
            var roomId = "room-1";
            _roomManager.AddMessage(roomId, new ChatMessage("user-1", "User1", null, "Message 1"));
            _roomManager.AddMessage(roomId, new ChatMessage("user-2", "User2", null, "Message 2"));
            _roomManager.AddMessage(roomId, new ChatMessage("user-1", "User1", null, "Message 3"));

            // Act
            var result = await _chatService.GetChatHistoryAsync(roomId);

            // Assert
            result.Should().HaveCount(3);
            result.First().Content.Should().Be("Message 1");
            result.Last().Content.Should().Be("Message 3");
        }

        [Fact]
        public async Task GetChatHistoryAsync_WithNoMessages_ShouldReturnEmptyList()
        {
            // Arrange
            var roomId = "room-1";

            // Act
            var result = await _chatService.GetChatHistoryAsync(roomId);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatHistoryAsync_ShouldMapToDtosCorrectly()
        {
            // Arrange
            var roomId = "room-1";
            
            var message = new ChatMessage("user-1", "TestUser", "https://avatar.com/1.png", "Test content");
            _roomManager.AddMessage(roomId, message);

            // Act
            var result = await _chatService.GetChatHistoryAsync(roomId);

            // Assert
            result.Should().HaveCount(1);
            var dto = result.First();
            dto.SenderId.Should().Be("user-1");
            dto.SenderName.Should().Be("TestUser");
            dto.AvatarUrl.Should().Be("https://avatar.com/1.png");
            dto.Content.Should().Be("Test content");
            dto.Id.Should().NotBeNullOrEmpty();
            dto.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        #endregion

        #region SendChatHistoryToClientAsync Tests

        [Fact]
        public async Task SendChatHistoryToClientAsync_ShouldSendHistoryToSpecificClient()
        {
            // Arrange
            var connectionId = "conn-123";
            var roomId = "room-1";
            _roomManager.AddMessage(roomId, new ChatMessage("user-1", "User1", null, "Message 1"));
            _roomManager.AddMessage(roomId, new ChatMessage("user-2", "User2", null, "Message 2"));

            // Act
            await _chatService.SendChatHistoryToClientAsync(connectionId, roomId);

            // Assert
            _mockClientProxy.Verify(
                c => c.ReceiveChatHistory(It.Is<IEnumerable<ChatMessageDto>>(msgs => msgs.Count() == 2)),
                Times.Once);
        }

        [Fact]
        public async Task SendChatHistoryToClientAsync_WithEmptyHistory_ShouldSendEmptyList()
        {
            // Arrange
            var connectionId = "conn-123";
            var roomId = "room-1";

            // Act
            await _chatService.SendChatHistoryToClientAsync(connectionId, roomId);

            // Assert
            _mockClientProxy.Verify(
                c => c.ReceiveChatHistory(It.Is<IEnumerable<ChatMessageDto>>(msgs => !msgs.Any())),
                Times.Once);
        }

        #endregion

        #region Edge Cases and Error Handling

        [Fact]
        public async Task SendMessageAsync_WhenBroadcastFails_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-1";
            var senderId = "user-1";
            var senderName = "TestUser";
            var content = "Test message";

            _mockClientProxy
                .Setup(c => c.ReceiveMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), 
                    It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception("Broadcast failed"));

            // Act
            var result = await _chatService.SendMessageAsync(roomId, senderId, senderName, null, content);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task SendSystemMessageAsync_WhenBroadcastFails_ShouldNotThrow()
        {
            // Arrange
            var roomId = "room-1";
            var content = "System message";

            _mockClientProxy
                .Setup(c => c.ReceiveMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), 
                    It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception("Broadcast failed"));

            // Act & Assert - Should not throw
            await _chatService.Invoking(s => s.SendSystemMessageAsync(roomId, content))
                .Should().NotThrowAsync();
        }

        [Fact]
        public async Task SendChatHistoryToClientAsync_WhenSendFails_ShouldNotThrow()
        {
            // Arrange
            var connectionId = "conn-123";
            var roomId = "room-1";
            _mockClientProxy
                .Setup(c => c.ReceiveChatHistory(It.IsAny<IEnumerable<ChatMessageDto>>()))
                .ThrowsAsync(new Exception("Send failed"));

            // Act & Assert - Should not throw
            await _chatService.Invoking(s => s.SendChatHistoryToClientAsync(connectionId, roomId))
                .Should().NotThrowAsync();
        }

        #endregion
    }
}
