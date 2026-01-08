using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StreamSync.Services.Redis;
using StreamSync.Models.InMemory;
using System.Text.Json;

namespace StreamSync.Tests.Services
{
    public class RedisRoomStateServiceTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDb;
        private readonly Mock<ILogger<RedisRoomStateService>> _mockLogger;
        private readonly RedisRoomStateService _redisService;

        public RedisRoomStateServiceTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDb = new Mock<IDatabase>();
            _mockLogger = new Mock<ILogger<RedisRoomStateService>>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDb.Object);

            _redisService = new RedisRoomStateService(_mockRedis.Object, _mockLogger.Object);
        }

        #region AddParticipantAsync Tests

        [Fact]
        public async Task AddParticipantAsync_ShouldStoreParticipantInHash()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", "avatar.jpg", true);

            _mockDb.Setup(db => db.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            _mockDb.Setup(db => db.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            _mockDb.Setup(db => db.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            _mockDb.Setup(db => db.SetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.AddParticipantAsync(roomId, participant);

            // Assert
            _mockDb.Verify(db => db.HashSetAsync(
                "room:room-123:participants",
                "user-1",
                It.Is<RedisValue>(v => v.ToString().Contains("User One")),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);

            _mockDb.Verify(db => db.SetAddAsync(
                "active_rooms",
                "room-123",
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task AddParticipantAsync_ShouldResetMessageExpiry_WhenMessagesExist()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", null);

            _mockDb.Setup(db => db.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            _mockDb.Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            _mockDb.Setup(db => db.KeyExistsAsync("room:room-123:messages", It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            _mockDb.Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.AddParticipantAsync(roomId, participant);

            // Assert - Should reset expiry on messages key
            _mockDb.Verify(db => db.KeyExpireAsync(
                "room:room-123:messages",
                TimeSpan.FromHours(24),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion

        #region RemoveParticipantAsync Tests

        [Fact]
        public async Task RemoveParticipantAsync_ShouldRemoveFromHash()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "user-1";

            _mockDb.Setup(db => db.HashDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            _mockDb.Setup(db => db.HashLengthAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(1); // Room not empty

            // Act
            await _redisService.RemoveParticipantAsync(roomId, participantId);

            // Assert
            _mockDb.Verify(db => db.HashDeleteAsync(
                "room:room-123:participants",
                "user-1",
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task RemoveParticipantAsync_WhenRoomEmpty_ShouldSetExpiry()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "user-1";

            _mockDb.Setup(db => db.HashDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            _mockDb.Setup(db => db.HashLengthAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(0); // Room is now empty
            _mockDb.Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            _mockDb.Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.RemoveParticipantAsync(roomId, participantId);

            // Assert - Should remove from active rooms and set 3-hour expiry
            _mockDb.Verify(db => db.SetRemoveAsync(
                "active_rooms",
                "room-123",
                It.IsAny<CommandFlags>()), Times.Once);

            _mockDb.Verify(db => db.KeyExpireAsync(
                "room:room-123:participants",
                TimeSpan.FromHours(3),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()), Times.Once);

            _mockDb.Verify(db => db.KeyExpireAsync(
                "room:room-123:messages",
                TimeSpan.FromHours(3),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()), Times.Once);
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
            var json = JsonSerializer.Serialize(participant);

            _mockDb.Setup(db => db.HashGetAsync(
                "room:room-123:participants",
                "user-1",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(json));

            // Act
            var result = await _redisService.GetParticipantAsync(roomId, participantId);

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
            var participantId = "nonexistent";

            _mockDb.Setup(db => db.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _redisService.GetParticipantAsync(roomId, participantId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetRoomParticipantsAsync Tests

        [Fact]
        public async Task GetRoomParticipantsAsync_ShouldReturnOrderedParticipants()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null);
            
            // Make participant2 join earlier
            participant2.JoinedAt = participant1.JoinedAt.AddMinutes(-5);

            var entries = new HashEntry[]
            {
                new HashEntry("user-1", JsonSerializer.Serialize(participant1)),
                new HashEntry("user-2", JsonSerializer.Serialize(participant2))
            };

            _mockDb.Setup(db => db.HashGetAllAsync(
                "room:room-123:participants",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(entries);

            // Act
            var result = await _redisService.GetRoomParticipantsAsync(roomId);

            // Assert
            result.Should().HaveCount(2);
            result[0].Id.Should().Be("user-2"); // Earlier joiner first
            result[1].Id.Should().Be("user-1");
        }

        [Fact]
        public async Task GetRoomParticipantsAsync_WithEmptyRoom_ShouldReturnEmptyList()
        {
            // Arrange
            var roomId = "room-123";

            _mockDb.Setup(db => db.HashGetAllAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(Array.Empty<HashEntry>());

            // Act
            var result = await _redisService.GetRoomParticipantsAsync(roomId);

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
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, false);
            var participant2 = new RoomParticipant("user-2", "conn-2", "Controller", null, true);

            var entries = new HashEntry[]
            {
                new HashEntry("user-1", JsonSerializer.Serialize(participant1)),
                new HashEntry("user-2", JsonSerializer.Serialize(participant2))
            };

            _mockDb.Setup(db => db.HashGetAllAsync(
                "room:room-123:participants",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(entries);

            // Act
            var result = await _redisService.GetControllerAsync(roomId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("user-2");
            result.HasControl.Should().BeTrue();
        }

        [Fact]
        public async Task GetControllerAsync_WithNoController_ShouldReturnNull()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", null, false);

            var entries = new HashEntry[]
            {
                new HashEntry("user-1", JsonSerializer.Serialize(participant))
            };

            _mockDb.Setup(db => db.HashGetAllAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(entries);

            // Act
            var result = await _redisService.GetControllerAsync(roomId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region SetControllerAsync Tests

        [Fact]
        public async Task SetControllerAsync_ShouldUpdateAllParticipants()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, true);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, false);

            var entries = new HashEntry[]
            {
                new HashEntry("user-1", JsonSerializer.Serialize(participant1)),
                new HashEntry("user-2", JsonSerializer.Serialize(participant2))
            };

            _mockDb.Setup(db => db.HashGetAllAsync(
                "room:room-123:participants",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(entries);

            _mockDb.Setup(db => db.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.SetControllerAsync(roomId, "user-2");

            // Assert - Both participants should be updated
            _mockDb.Verify(db => db.HashSetAsync(
                "room:room-123:participants",
                "user-1",
                It.Is<RedisValue>(v => v.ToString().Contains("\"HasControl\":false")),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);

            _mockDb.Verify(db => db.HashSetAsync(
                "room:room-123:participants",
                "user-2",
                It.Is<RedisValue>(v => v.ToString().Contains("\"HasControl\":true")),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion

        #region GetParticipantCountAsync Tests

        [Fact]
        public async Task GetParticipantCountAsync_ShouldReturnHashLength()
        {
            // Arrange
            var roomId = "room-123";

            _mockDb.Setup(db => db.HashLengthAsync(
                "room:room-123:participants",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(5);

            // Act
            var result = await _redisService.GetParticipantCountAsync(roomId);

            // Assert
            result.Should().Be(5);
        }

        [Fact]
        public async Task GetParticipantCountAsync_ForEmptyRoom_ShouldReturnZero()
        {
            // Arrange
            var roomId = "nonexistent";

            _mockDb.Setup(db => db.HashLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(0);

            // Act
            var result = await _redisService.GetParticipantCountAsync(roomId);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region IsParticipantInRoomAsync Tests

        [Fact]
        public async Task IsParticipantInRoomAsync_WithExistingParticipant_ShouldReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "user-1";

            _mockDb.Setup(db => db.HashExistsAsync(
                "room:room-123:participants",
                "user-1",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var result = await _redisService.IsParticipantInRoomAsync(roomId, participantId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsParticipantInRoomAsync_WithNonExistentParticipant_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "nonexistent";

            _mockDb.Setup(db => db.HashExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act
            var result = await _redisService.IsParticipantInRoomAsync(roomId, participantId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region AddMessageAsync Tests

        [Fact]
        public async Task AddMessageAsync_ShouldAddToListAndTrim()
        {
            // Arrange
            var roomId = "room-123";
            var message = new ChatMessage("user-1", "User One", "avatar.jpg", "Hello!");

            _mockDb.Setup(db => db.ListRightPushAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(1);

            _mockDb.Setup(db => db.ListTrimAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
                .Returns(Task.CompletedTask);

            _mockDb.Setup(db => db.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.AddMessageAsync(roomId, message);

            // Assert
            _mockDb.Verify(db => db.ListRightPushAsync(
                "room:room-123:messages",
                It.Is<RedisValue>(v => v.ToString().Contains("Hello!")),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);

            _mockDb.Verify(db => db.ListTrimAsync(
                "room:room-123:messages",
                -50, // MAX_MESSAGES_PER_ROOM
                -1,
                It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion

        #region GetRoomMessagesAsync Tests

        [Fact]
        public async Task GetRoomMessagesAsync_ShouldReturnAllMessages()
        {
            // Arrange
            var roomId = "room-123";
            var message1 = new ChatMessage("user-1", "User One", null, "Message 1");
            var message2 = new ChatMessage("user-2", "User Two", null, "Message 2");

            var values = new RedisValue[]
            {
                new RedisValue(JsonSerializer.Serialize(message1)),
                new RedisValue(JsonSerializer.Serialize(message2))
            };

            _mockDb.Setup(db => db.ListRangeAsync(
                "room:room-123:messages",
                0,
                -1,
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(values);

            // Act
            var result = await _redisService.GetRoomMessagesAsync(roomId);

            // Assert
            result.Should().HaveCount(2);
            result[0].Content.Should().Be("Message 1");
            result[1].Content.Should().Be("Message 2");
        }

        [Fact]
        public async Task GetRoomMessagesAsync_WithNoMessages_ShouldReturnEmptyList()
        {
            // Arrange
            var roomId = "room-123";

            _mockDb.Setup(db => db.ListRangeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(Array.Empty<RedisValue>());

            // Act
            var result = await _redisService.GetRoomMessagesAsync(roomId);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region ClearRoomDataAsync Tests

        [Fact]
        public async Task ClearRoomDataAsync_ShouldDeleteAllKeys()
        {
            // Arrange
            var roomId = "room-123";

            _mockDb.Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey[]>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(2);

            _mockDb.Setup(db => db.SetRemoveAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.ClearRoomDataAsync(roomId);

            // Assert
            _mockDb.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey[]>(keys => 
                    keys.Contains((RedisKey)"room:room-123:participants") && 
                    keys.Contains((RedisKey)"room:room-123:messages")),
                It.IsAny<CommandFlags>()), Times.Once);

            _mockDb.Verify(db => db.SetRemoveAsync(
                "active_rooms",
                "room-123",
                It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion

        #region GetActiveRoomIdsAsync Tests

        [Fact]
        public async Task GetActiveRoomIdsAsync_ShouldReturnAllRoomIds()
        {
            // Arrange
            var members = new RedisValue[] { "room-1", "room-2", "room-3" };

            _mockDb.Setup(db => db.SetMembersAsync(
                "active_rooms",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(members);

            // Act
            var result = await _redisService.GetActiveRoomIdsAsync();

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(new[] { "room-1", "room-2", "room-3" });
        }

        [Fact]
        public async Task GetActiveRoomIdsAsync_WithNoActiveRooms_ShouldReturnEmptyList()
        {
            // Arrange
            _mockDb.Setup(db => db.SetMembersAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(Array.Empty<RedisValue>());

            // Act
            var result = await _redisService.GetActiveRoomIdsAsync();

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region UpdateParticipantConnectionIdAsync Tests

        [Fact]
        public async Task UpdateParticipantConnectionIdAsync_ShouldUpdateConnectionId()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "user-1";
            var newConnectionId = "new-conn-123";
            var participant = new RoomParticipant(participantId, "old-conn", "User One", null);

            _mockDb.Setup(db => db.HashGetAsync(
                "room:room-123:participants",
                "user-1",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(JsonSerializer.Serialize(participant)));

            _mockDb.Setup(db => db.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.UpdateParticipantConnectionIdAsync(roomId, participantId, newConnectionId);

            // Assert
            _mockDb.Verify(db => db.HashSetAsync(
                "room:room-123:participants",
                "user-1",
                It.Is<RedisValue>(v => v.ToString().Contains("new-conn-123")),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task UpdateParticipantConnectionIdAsync_WithNonExistentParticipant_ShouldNotUpdate()
        {
            // Arrange
            var roomId = "room-123";
            var participantId = "nonexistent";

            _mockDb.Setup(db => db.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            // Act
            await _redisService.UpdateParticipantConnectionIdAsync(roomId, participantId, "new-conn");

            // Assert
            _mockDb.Verify(db => db.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Never);
        }

        #endregion

        #region TransferControlToNextAsync Tests

        [Fact]
        public async Task TransferControlToNextAsync_ShouldTransferToOldestRemaining()
        {
            // Arrange
            var roomId = "room-123";
            var current = new RoomParticipant("user-1", "conn-1", "Current", null, true);
            var next = new RoomParticipant("user-2", "conn-2", "Next", null, false);
            next.JoinedAt = current.JoinedAt.AddMinutes(-5); // next joined earlier

            var entries = new HashEntry[]
            {
                new HashEntry("user-1", JsonSerializer.Serialize(current)),
                new HashEntry("user-2", JsonSerializer.Serialize(next))
            };

            _mockDb.Setup(db => db.HashGetAllAsync(
                "room:room-123:participants",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(entries);

            _mockDb.Setup(db => db.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.TransferControlToNextAsync(roomId, "user-1");

            // Assert - user-2 should get control
            _mockDb.Verify(db => db.HashSetAsync(
                "room:room-123:participants",
                "user-2",
                It.Is<RedisValue>(v => v.ToString().Contains("\"HasControl\":true")),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion

        #region EnsureControlConsistencyAsync Tests

        [Fact]
        public async Task EnsureControlConsistencyAsync_WithNoController_ShouldAssignToOldest()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, false);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, false);
            participant1.JoinedAt = participant2.JoinedAt.AddMinutes(-5); // user-1 joined earlier

            var entries = new HashEntry[]
            {
                new HashEntry("user-1", JsonSerializer.Serialize(participant1)),
                new HashEntry("user-2", JsonSerializer.Serialize(participant2))
            };

            _mockDb.Setup(db => db.HashGetAllAsync(
                "room:room-123:participants",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(entries);

            _mockDb.Setup(db => db.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.EnsureControlConsistencyAsync(roomId);

            // Assert - oldest (user-1) should get control
            _mockDb.Verify(db => db.HashSetAsync(
                "room:room-123:participants",
                "user-1",
                It.Is<RedisValue>(v => v.ToString().Contains("\"HasControl\":true")),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task EnsureControlConsistencyAsync_WithMultipleControllers_ShouldKeepOnlyOldest()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, true);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, true);
            participant1.JoinedAt = participant2.JoinedAt.AddMinutes(-5); // user-1 joined earlier

            var entries = new HashEntry[]
            {
                new HashEntry("user-1", JsonSerializer.Serialize(participant1)),
                new HashEntry("user-2", JsonSerializer.Serialize(participant2))
            };

            _mockDb.Setup(db => db.HashGetAllAsync(
                "room:room-123:participants",
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(entries);

            _mockDb.Setup(db => db.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.EnsureControlConsistencyAsync(roomId);

            // Assert - user-2 should lose control
            _mockDb.Verify(db => db.HashSetAsync(
                "room:room-123:participants",
                "user-2",
                It.Is<RedisValue>(v => v.ToString().Contains("\"HasControl\":false")),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion

        #region CleanupEmptyRoomsAsync Tests

        [Fact]
        public async Task CleanupEmptyRoomsAsync_ShouldCleanupEmptyRooms()
        {
            // Arrange
            var members = new RedisValue[] { "room-1", "room-2" };

            _mockDb.Setup(db => db.SetMembersAsync("active_rooms", It.IsAny<CommandFlags>()))
                .ReturnsAsync(members);

            // room-1 has participants, room-2 is empty
            _mockDb.Setup(db => db.HashLengthAsync("room:room-1:participants", It.IsAny<CommandFlags>()))
                .ReturnsAsync(2);
            _mockDb.Setup(db => db.HashLengthAsync("room:room-2:participants", It.IsAny<CommandFlags>()))
                .ReturnsAsync(0);

            _mockDb.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(2);
            _mockDb.Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _redisService.CleanupEmptyRoomsAsync();

            // Assert - Only room-2 should be cleaned up
            _mockDb.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey[]>(keys => 
                    keys.Contains((RedisKey)"room:room-2:participants") &&
                    keys.Contains((RedisKey)"room:room-2:messages")),
                It.IsAny<CommandFlags>()), Times.Once);

            // room-1 should NOT be cleaned up
            _mockDb.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey[]>(keys => 
                    keys.Contains((RedisKey)"room:room-1:participants")),
                It.IsAny<CommandFlags>()), Times.Never);
        }

        #endregion
    }
}
