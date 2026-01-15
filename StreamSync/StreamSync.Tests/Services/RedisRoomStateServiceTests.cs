using Microsoft.Extensions.Logging;
using StreamSync.DataAccess.Interfaces;
using StreamSync.Services.Redis;
using StreamSync.Models.RealTime;

namespace StreamSync.Tests.Services
{
    public class RedisRoomStateServiceTests
    {
        private readonly Mock<ICacheService> _mockCache;
        private readonly Mock<ILogger<RedisRoomStateService>> _mockLogger;
        private readonly RedisRoomStateService _service;

        public RedisRoomStateServiceTests()
        {
            _mockCache = new Mock<ICacheService>();
            _mockLogger = new Mock<ILogger<RedisRoomStateService>>();

            _service = new RedisRoomStateService(_mockCache.Object, _mockLogger.Object);
        }

        #region AddParticipantAsync Tests

        [Fact]
        public async Task AddParticipantAsync_ShouldStoreParticipantInHash()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", "avatar.jpg", true);

            _mockCache.Setup(c => c.HashSetAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<RoomParticipant>(), 
                It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            _mockCache.Setup(c => c.ExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            _mockCache.Setup(c => c.SetAddAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.AddParticipantAsync(roomId, participant);

            // Assert
            _mockCache.Verify(c => c.HashSetAsync(
                "roomstate:room-123:participants",
                "user-1",
                It.Is<RoomParticipant>(p => p.DisplayName == "User One"),
                TimeSpan.FromHours(24)), Times.Once);

            _mockCache.Verify(c => c.SetAddAsync(
                "roomstate:active_rooms",
                "room-123"), Times.Once);
        }

        [Fact]
        public async Task AddParticipantAsync_ShouldResetMessageExpiry_WhenMessagesExist()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", null);

            _mockCache.Setup(c => c.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RoomParticipant>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);
            _mockCache.Setup(c => c.ExistsAsync("roomstate:room-123:messages"))
                .ReturnsAsync(true);
            _mockCache.Setup(c => c.SetExpirationAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);
            _mockCache.Setup(c => c.SetAddAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.AddParticipantAsync(roomId, participant);

            // Assert - Should reset expiry on messages key
            _mockCache.Verify(c => c.SetExpirationAsync(
                "roomstate:room-123:messages",
                TimeSpan.FromHours(24)), Times.Once);
        }

        #endregion

        #region RemoveParticipantAsync Tests

        [Fact]
        public async Task RemoveParticipantAsync_ShouldRemoveFromHash()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "user-1";

            _mockCache.Setup(c => c.HashRemoveAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockCache.Setup(c => c.HashLengthAsync(It.IsAny<string>()))
                .ReturnsAsync(1); // Room not empty

            // Act
            await _service.RemoveParticipantAsync(roomId, participantId);

            // Assert
            _mockCache.Verify(c => c.HashRemoveAsync(
                "roomstate:room-123:participants",
                "user-1"), Times.Once);
        }

        [Fact]
        public async Task RemoveParticipantAsync_WhenRoomEmpty_ShouldSetExpiry()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "user-1";

            _mockCache.Setup(c => c.HashRemoveAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockCache.Setup(c => c.HashLengthAsync(It.IsAny<string>()))
                .ReturnsAsync(0); // Room is now empty
            _mockCache.Setup(c => c.SetRemoveAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockCache.Setup(c => c.SetExpirationAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.RemoveParticipantAsync(roomId, participantId);

            // Assert - Should remove from active rooms and set 3-hour expiry
            _mockCache.Verify(c => c.SetRemoveAsync(
                "roomstate:active_rooms",
                "room-123"), Times.Once);

            _mockCache.Verify(c => c.SetExpirationAsync(
                "roomstate:room-123:participants",
                TimeSpan.FromHours(3)), Times.Once);

            _mockCache.Verify(c => c.SetExpirationAsync(
                "roomstate:room-123:messages",
                TimeSpan.FromHours(3)), Times.Once);
        }

        #endregion

        #region GetParticipantAsync Tests

        [Fact]
        public async Task GetParticipantAsync_WithExistingParticipant_ShouldReturnParticipant()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "user-1";
            var participant = new RoomParticipant(participantId, "conn-1", "User One", "avatar.jpg", true);

            _mockCache.Setup(c => c.HashGetAsync<RoomParticipant>(
                "roomstate:room-123:participants",
                "user-1"))
                .ReturnsAsync(participant);

            // Act
            var result = await _service.GetParticipantAsync(roomId, participantId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(participantId);
            result.DisplayName.Should().Be("User One");
            result.HasControl.Should().BeTrue();
        }

        [Fact]
        public async Task GetParticipantAsync_WithNonExistentParticipant_ShouldReturnNull()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "non-existent";

            _mockCache.Setup(c => c.HashGetAsync<RoomParticipant>(
                "roomstate:room-123:participants",
                "non-existent"))
                .ReturnsAsync((RoomParticipant?)null);

            // Act
            var result = await _service.GetParticipantAsync(roomId, participantId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetRoomParticipantsAsync Tests

        [Fact]
        public async Task GetRoomParticipantsAsync_ShouldReturnOrderedByJoinTime()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "First", null, true) 
            { 
                JoinedAt = DateTime.UtcNow.AddMinutes(-10) 
            };
            var participant2 = new RoomParticipant("user-2", "conn-2", "Second", null, false) 
            { 
                JoinedAt = DateTime.UtcNow.AddMinutes(-5) 
            };

            var participants = new Dictionary<string, RoomParticipant>
            {
                { "user-2", participant2 },
                { "user-1", participant1 }
            };

            _mockCache.Setup(c => c.HashGetAllAsync<RoomParticipant>("roomstate:room-123:participants"))
                .ReturnsAsync(participants);

            // Act
            var result = await _service.GetRoomParticipantsAsync(roomId);

            // Assert
            result.Should().HaveCount(2);
            result[0].Id.Should().Be("user-1"); // Joined first
            result[1].Id.Should().Be("user-2"); // Joined second
        }

        [Fact]
        public async Task GetRoomParticipantsAsync_WithEmptyRoom_ShouldReturnEmptyList()
        {
            // Arrange
            var roomId = "room-123";

            _mockCache.Setup(c => c.HashGetAllAsync<RoomParticipant>("roomstate:room-123:participants"))
                .ReturnsAsync(new Dictionary<string, RoomParticipant>());

            // Act
            var result = await _service.GetRoomParticipantsAsync(roomId);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region GetControllerAsync Tests

        [Fact]
        public async Task GetControllerAsync_ShouldReturnParticipantWithControl()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "First", null, false);
            var participant2 = new RoomParticipant("user-2", "conn-2", "Controller", null, true);

            var participants = new Dictionary<string, RoomParticipant>
            {
                { "user-1", participant1 },
                { "user-2", participant2 }
            };

            _mockCache.Setup(c => c.HashGetAllAsync<RoomParticipant>("roomstate:room-123:participants"))
                .ReturnsAsync(participants);

            // Act
            var result = await _service.GetControllerAsync(roomId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("user-2");
            result.HasControl.Should().BeTrue();
        }

        [Fact]
        public async Task GetControllerAsync_WhenNoController_ShouldReturnNull()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "First", null, false);
            var participant2 = new RoomParticipant("user-2", "conn-2", "Second", null, false);

            var participants = new Dictionary<string, RoomParticipant>
            {
                { "user-1", participant1 },
                { "user-2", participant2 }
            };

            _mockCache.Setup(c => c.HashGetAllAsync<RoomParticipant>("roomstate:room-123:participants"))
                .ReturnsAsync(participants);

            // Act
            var result = await _service.GetControllerAsync(roomId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region SetControllerAsync Tests

        [Fact]
        public async Task SetControllerAsync_ShouldSetCorrectParticipantAsController()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "First", null, true);
            var participant2 = new RoomParticipant("user-2", "conn-2", "Second", null, false);

            var participants = new Dictionary<string, RoomParticipant>
            {
                { "user-1", participant1 },
                { "user-2", participant2 }
            };

            _mockCache.Setup(c => c.HashGetAllAsync<RoomParticipant>("roomstate:room-123:participants"))
                .ReturnsAsync(participants);

            _mockCache.Setup(c => c.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RoomParticipant>(),
                It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.SetControllerAsync(roomId, "user-2");

            // Assert - user-1 should lose control, user-2 should gain control
            _mockCache.Verify(c => c.HashSetAsync(
                "roomstate:room-123:participants",
                "user-1",
                It.Is<RoomParticipant>(p => !p.HasControl),
                null), Times.Once);

            _mockCache.Verify(c => c.HashSetAsync(
                "roomstate:room-123:participants",
                "user-2",
                It.Is<RoomParticipant>(p => p.HasControl),
                null), Times.Once);
        }

        #endregion

        #region TransferControlToNextAsync Tests

        [Fact]
        public async Task TransferControlToNextAsync_ShouldTransferToOldestRemainingParticipant()
        {
            // Arrange
            var roomId = "room-123";
            var now = DateTime.UtcNow;
            var participant1 = new RoomParticipant("user-1", "conn-1", "First", null, true) 
            { 
                JoinedAt = now.AddMinutes(-10) 
            };
            var participant2 = new RoomParticipant("user-2", "conn-2", "Second", null, false) 
            { 
                JoinedAt = now.AddMinutes(-5) 
            };
            var participant3 = new RoomParticipant("user-3", "conn-3", "Third", null, false) 
            { 
                JoinedAt = now.AddMinutes(-3) 
            };

            var participants = new Dictionary<string, RoomParticipant>
            {
                { "user-1", participant1 },
                { "user-2", participant2 },
                { "user-3", participant3 }
            };

            _mockCache.Setup(c => c.HashGetAllAsync<RoomParticipant>("roomstate:room-123:participants"))
                .ReturnsAsync(participants);

            _mockCache.Setup(c => c.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RoomParticipant>(),
                It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.TransferControlToNextAsync(roomId, "user-1");

            // Assert - user-2 should get control (oldest remaining)
            // The method first sets HasControl=false on all, then sets HasControl=true on user-2
            // So user-2 is called twice (once to remove, once to grant)
            _mockCache.Verify(c => c.HashSetAsync(
                "roomstate:room-123:participants",
                "user-2",
                It.Is<RoomParticipant>(p => p.HasControl),
                null), Times.AtLeastOnce);
        }

        #endregion

        #region IsParticipantInRoomAsync Tests

        [Fact]
        public async Task IsParticipantInRoomAsync_WithExistingParticipant_ShouldReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "user-1";

            _mockCache.Setup(c => c.HashExistsAsync("roomstate:room-123:participants", "user-1"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsParticipantInRoomAsync(roomId, participantId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsParticipantInRoomAsync_WithNonExistentParticipant_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "non-existent";

            _mockCache.Setup(c => c.HashExistsAsync("roomstate:room-123:participants", "non-existent"))
                .ReturnsAsync(false);

            // Act
            var result = await _service.IsParticipantInRoomAsync(roomId, participantId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetParticipantCountAsync Tests

        [Fact]
        public async Task GetParticipantCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            var roomId = "room-123";

            _mockCache.Setup(c => c.HashLengthAsync("roomstate:room-123:participants"))
                .ReturnsAsync(5);

            // Act
            var result = await _service.GetParticipantCountAsync(roomId);

            // Assert
            result.Should().Be(5);
        }

        #endregion

        #region Chat Management Tests

        [Fact]
        public async Task AddMessageAsync_ShouldAddMessageToList()
        {
            // Arrange
            var roomId = "room-123";
            var message = new ChatMessage("user-1", "User One", null, "Hello!");

            _mockCache.Setup(c => c.ListPushAsync(
                It.IsAny<string>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<int?>(),
                It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.AddMessageAsync(roomId, message);

            // Assert
            _mockCache.Verify(c => c.ListPushAsync(
                "roomstate:room-123:messages",
                It.Is<ChatMessage>(m => m.Content == "Hello!"),
                50,  // MAX_MESSAGES_PER_ROOM
                TimeSpan.FromHours(24)), Times.Once);
        }

        [Fact]
        public async Task GetRoomMessagesAsync_ShouldReturnMessages()
        {
            // Arrange
            var roomId = "room-123";
            var messages = new List<ChatMessage>
            {
                new ChatMessage("user-1", "User One", null, "First"),
                new ChatMessage("user-2", "User Two", null, "Second")
            };

            _mockCache.Setup(c => c.ListRangeAsync<ChatMessage>("roomstate:room-123:messages", 0, -1))
                .ReturnsAsync(messages);

            // Act
            var result = await _service.GetRoomMessagesAsync(roomId);

            // Assert
            result.Should().HaveCount(2);
            result[0].Content.Should().Be("First");
            result[1].Content.Should().Be("Second");
        }

        #endregion

        #region Room Cleanup Tests

        [Fact]
        public async Task ClearRoomDataAsync_ShouldRemoveAllRoomData()
        {
            // Arrange
            var roomId = "room-123";

            _mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockCache.Setup(c => c.SetRemoveAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.ClearRoomDataAsync(roomId);

            // Assert
            _mockCache.Verify(c => c.RemoveAsync("roomstate:room-123:participants"), Times.Once);
            _mockCache.Verify(c => c.RemoveAsync("roomstate:room-123:messages"), Times.Once);
            _mockCache.Verify(c => c.SetRemoveAsync("roomstate:active_rooms", "room-123"), Times.Once);
        }

        [Fact]
        public async Task GetActiveRoomIdsAsync_ShouldReturnActiveRoomIds()
        {
            // Arrange
            var roomIds = new List<string> { "room-1", "room-2", "room-3" };

            _mockCache.Setup(c => c.SetMembersAsync("roomstate:active_rooms"))
                .ReturnsAsync(roomIds);

            // Act
            var result = await _service.GetActiveRoomIdsAsync();

            // Assert
            result.Should().BeEquivalentTo(roomIds);
        }

        [Fact]
        public async Task CleanupEmptyRoomsAsync_ShouldOnlyCleanupEmptyRooms()
        {
            // Arrange
            var roomIds = new List<string> { "room-1", "room-2" };

            _mockCache.Setup(c => c.SetMembersAsync("roomstate:active_rooms"))
                .ReturnsAsync(roomIds);

            // room-1 has participants, room-2 is empty
            _mockCache.Setup(c => c.HashLengthAsync("roomstate:room-1:participants"))
                .ReturnsAsync(2);
            _mockCache.Setup(c => c.HashLengthAsync("roomstate:room-2:participants"))
                .ReturnsAsync(0);

            _mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockCache.Setup(c => c.SetRemoveAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.CleanupEmptyRoomsAsync();

            // Assert - Only room-2 should be cleaned up
            _mockCache.Verify(c => c.RemoveAsync("roomstate:room-2:participants"), Times.Once);
            _mockCache.Verify(c => c.RemoveAsync("roomstate:room-2:messages"), Times.Once);

            // room-1 should NOT be cleaned up
            _mockCache.Verify(c => c.RemoveAsync("roomstate:room-1:participants"), Times.Never);
            _mockCache.Verify(c => c.RemoveAsync("roomstate:room-1:messages"), Times.Never);
        }

        #endregion

        #region EnsureControlConsistencyAsync Tests

        [Fact]
        public async Task EnsureControlConsistencyAsync_WithNoController_ShouldAssignToOldest()
        {
            // Arrange
            var roomId = "room-123";
            var now = DateTime.UtcNow;
            var participant1 = new RoomParticipant("user-1", "conn-1", "First", null, false) 
            { 
                JoinedAt = now.AddMinutes(-10) 
            };
            var participant2 = new RoomParticipant("user-2", "conn-2", "Second", null, false) 
            { 
                JoinedAt = now.AddMinutes(-5) 
            };

            var participants = new Dictionary<string, RoomParticipant>
            {
                { "user-1", participant1 },
                { "user-2", participant2 }
            };

            _mockCache.Setup(c => c.HashGetAllAsync<RoomParticipant>("roomstate:room-123:participants"))
                .ReturnsAsync(participants);

            _mockCache.Setup(c => c.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RoomParticipant>(),
                It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.EnsureControlConsistencyAsync(roomId);

            // Assert - user-1 should get control (oldest)
            _mockCache.Verify(c => c.HashSetAsync(
                "roomstate:room-123:participants",
                "user-1",
                It.Is<RoomParticipant>(p => p.HasControl),
                null), Times.Once);
        }

        [Fact]
        public async Task EnsureControlConsistencyAsync_WithMultipleControllers_ShouldKeepOnlyOldest()
        {
            // Arrange
            var roomId = "room-123";
            var now = DateTime.UtcNow;
            var participant1 = new RoomParticipant("user-1", "conn-1", "First", null, true) 
            { 
                JoinedAt = now.AddMinutes(-10) 
            };
            var participant2 = new RoomParticipant("user-2", "conn-2", "Second", null, true) 
            { 
                JoinedAt = now.AddMinutes(-5) 
            };

            var participants = new Dictionary<string, RoomParticipant>
            {
                { "user-1", participant1 },
                { "user-2", participant2 }
            };

            _mockCache.Setup(c => c.HashGetAllAsync<RoomParticipant>("roomstate:room-123:participants"))
                .ReturnsAsync(participants);

            _mockCache.Setup(c => c.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RoomParticipant>(),
                It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.EnsureControlConsistencyAsync(roomId);

            // Assert - user-2 should lose control
            _mockCache.Verify(c => c.HashSetAsync(
                "roomstate:room-123:participants",
                "user-2",
                It.Is<RoomParticipant>(p => !p.HasControl),
                null), Times.Once);
        }

        #endregion

        #region UpdateParticipantConnectionIdAsync Tests

        [Fact]
        public async Task UpdateParticipantConnectionIdAsync_ShouldUpdateConnectionId()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "user-1";
            var participant = new RoomParticipant(participantId, "old-conn", "User One", null, false);

            _mockCache.Setup(c => c.HashGetAsync<RoomParticipant>(
                "roomstate:room-123:participants",
                "user-1"))
                .ReturnsAsync(participant);

            _mockCache.Setup(c => c.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RoomParticipant>(),
                It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpdateParticipantConnectionIdAsync(roomId, participantId, "new-conn");

            // Assert
            _mockCache.Verify(c => c.HashSetAsync(
                "roomstate:room-123:participants",
                "user-1",
                It.Is<RoomParticipant>(p => p.ConnectionId == "new-conn"),
                null), Times.Once);
        }

        #endregion
    }
}
