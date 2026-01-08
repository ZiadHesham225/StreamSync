using Microsoft.Extensions.Logging;
using StreamSync.Services.InMemory;
using StreamSync.Models.InMemory;

namespace StreamSync.Tests.Services
{
    public class InMemoryRoomStateServiceTests
    {
        private readonly InMemoryRoomStateService _roomStateService;
        private readonly Mock<ILogger<InMemoryRoomStateService>> _mockLogger;

        public InMemoryRoomStateServiceTests()
        {
            _mockLogger = new Mock<ILogger<InMemoryRoomStateService>>();
            _roomStateService = new InMemoryRoomStateService(_mockLogger.Object);
        }

        #region AddParticipant Tests

        [Fact]
        public async Task AddParticipantAsync_ShouldAddParticipantToRoom()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", null);

            // Act
            await _roomStateService.AddParticipantAsync(roomId, participant);

            // Assert
            var participants = await _roomStateService.GetRoomParticipantsAsync(roomId);
            participants.Should().ContainSingle();
            participants.First().Id.Should().Be("user-1");
        }

        [Fact]
        public async Task AddParticipantAsync_ShouldAddMultipleParticipants()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null);

            // Act
            await _roomStateService.AddParticipantAsync(roomId, participant1);
            await _roomStateService.AddParticipantAsync(roomId, participant2);

            // Assert
            var participants = await _roomStateService.GetRoomParticipantsAsync(roomId);
            participants.Should().HaveCount(2);
        }

        [Fact]
        public async Task AddParticipantAsync_ShouldUpdateExistingParticipant()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "Old Name", null);
            var updatedParticipant = new RoomParticipant("user-1", "conn-2", "New Name", "avatar.jpg");

            // Act
            await _roomStateService.AddParticipantAsync(roomId, participant);
            await _roomStateService.AddParticipantAsync(roomId, updatedParticipant);

            // Assert
            var participants = await _roomStateService.GetRoomParticipantsAsync(roomId);
            participants.Should().ContainSingle();
            participants.First().DisplayName.Should().Be("New Name");
            participants.First().ConnectionId.Should().Be("conn-2");
        }

        #endregion

        #region RemoveParticipant Tests

        [Fact]
        public async Task RemoveParticipantAsync_ShouldRemoveParticipantFromRoom()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", null);
            await _roomStateService.AddParticipantAsync(roomId, participant);

            // Act
            await _roomStateService.RemoveParticipantAsync(roomId, "user-1");

            // Assert
            var participants = await _roomStateService.GetRoomParticipantsAsync(roomId);
            participants.Should().BeEmpty();
        }

        [Fact]
        public async Task RemoveParticipantAsync_WhenLastParticipant_ShouldNotDeleteMessagesImmediately()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", null);
            await _roomStateService.AddParticipantAsync(roomId, participant);
            await _roomStateService.AddMessageAsync(roomId, new ChatMessage("user-1", "User One", null, "Hello"));

            // Act
            await _roomStateService.RemoveParticipantAsync(roomId, "user-1");

            // Assert - Messages should be retained (3 hour delay)
            var participants = await _roomStateService.GetRoomParticipantsAsync(roomId);
            participants.Should().BeEmpty();
            
            var messages = await _roomStateService.GetRoomMessagesAsync(roomId);
            messages.Should().ContainSingle(); // Messages should still exist
        }

        [Fact]
        public async Task RemoveParticipantAsync_FromNonExistentRoom_ShouldNotThrow()
        {
            // Act
            var action = async () => await _roomStateService.RemoveParticipantAsync("nonexistent", "user-1");

            // Assert
            await action.Should().NotThrowAsync();
        }

        #endregion

        #region GetParticipant Tests

        [Fact]
        public async Task GetParticipantAsync_WithExistingParticipant_ShouldReturnParticipant()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", "avatar.jpg", true);
            await _roomStateService.AddParticipantAsync(roomId, participant);

            // Act
            var result = await _roomStateService.GetParticipantAsync(roomId, "user-1");

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("user-1");
            result.DisplayName.Should().Be("User One");
            result.HasControl.Should().BeTrue();
        }

        [Fact]
        public async Task GetParticipantAsync_WithNonExistentParticipant_ShouldReturnNull()
        {
            // Arrange
            var roomId = "room-123";
            await _roomStateService.AddParticipantAsync(roomId, new RoomParticipant("user-1", "conn-1", "User One", null));

            // Act
            var result = await _roomStateService.GetParticipantAsync(roomId, "nonexistent");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetParticipantAsync_FromNonExistentRoom_ShouldReturnNull()
        {
            // Act
            var result = await _roomStateService.GetParticipantAsync("nonexistent", "user-1");

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetRoomParticipants Tests

        [Fact]
        public async Task GetRoomParticipantsAsync_ShouldReturnParticipantsOrderedByJoinTime()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null);
            await Task.Delay(10); // Ensure different join times
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null);
            await Task.Delay(10);
            var participant3 = new RoomParticipant("user-3", "conn-3", "User Three", null);

            await _roomStateService.AddParticipantAsync(roomId, participant1);
            await _roomStateService.AddParticipantAsync(roomId, participant2);
            await _roomStateService.AddParticipantAsync(roomId, participant3);

            // Act
            var participants = await _roomStateService.GetRoomParticipantsAsync(roomId);

            // Assert
            participants.Should().HaveCount(3);
            participants[0].Id.Should().Be("user-1");
            participants[1].Id.Should().Be("user-2");
            participants[2].Id.Should().Be("user-3");
        }

        [Fact]
        public async Task GetRoomParticipantsAsync_FromEmptyRoom_ShouldReturnEmptyList()
        {
            // Act
            var participants = await _roomStateService.GetRoomParticipantsAsync("nonexistent");

            // Assert
            participants.Should().BeEmpty();
        }

        #endregion

        #region Control Management Tests

        [Fact]
        public async Task GetControllerAsync_ShouldReturnParticipantWithControl()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, false);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, true);
            await _roomStateService.AddParticipantAsync(roomId, participant1);
            await _roomStateService.AddParticipantAsync(roomId, participant2);

            // Act
            var controller = await _roomStateService.GetControllerAsync(roomId);

            // Assert
            controller.Should().NotBeNull();
            controller!.Id.Should().Be("user-2");
        }

        [Fact]
        public async Task GetControllerAsync_WithNoController_ShouldReturnNull()
        {
            // Arrange
            var roomId = "room-123";
            await _roomStateService.AddParticipantAsync(roomId, new RoomParticipant("user-1", "conn-1", "User One", null, false));

            // Act
            var controller = await _roomStateService.GetControllerAsync(roomId);

            // Assert
            controller.Should().BeNull();
        }

        [Fact]
        public async Task SetControllerAsync_ShouldTransferControl()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, true);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, false);
            await _roomStateService.AddParticipantAsync(roomId, participant1);
            await _roomStateService.AddParticipantAsync(roomId, participant2);

            // Act
            await _roomStateService.SetControllerAsync(roomId, "user-2");

            // Assert
            var p1 = await _roomStateService.GetParticipantAsync(roomId, "user-1");
            var p2 = await _roomStateService.GetParticipantAsync(roomId, "user-2");
            p1!.HasControl.Should().BeFalse();
            p2!.HasControl.Should().BeTrue();
        }

        [Fact]
        public async Task TransferControlToNextAsync_ShouldGiveControlToOldestParticipant()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, true);
            await Task.Delay(10);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, false);
            await Task.Delay(10);
            var participant3 = new RoomParticipant("user-3", "conn-3", "User Three", null, false);
            
            await _roomStateService.AddParticipantAsync(roomId, participant1);
            await _roomStateService.AddParticipantAsync(roomId, participant2);
            await _roomStateService.AddParticipantAsync(roomId, participant3);

            // Act
            await _roomStateService.TransferControlToNextAsync(roomId, "user-1");

            // Assert
            var newController = await _roomStateService.GetControllerAsync(roomId);
            newController.Should().NotBeNull();
            newController!.Id.Should().Be("user-2");
            
            var oldController = await _roomStateService.GetParticipantAsync(roomId, "user-1");
            oldController!.HasControl.Should().BeFalse();
        }

        [Fact]
        public async Task EnsureControlConsistencyAsync_WithNoController_ShouldAssignToOldest()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, false);
            await Task.Delay(10);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, false);
            
            await _roomStateService.AddParticipantAsync(roomId, participant1);
            await _roomStateService.AddParticipantAsync(roomId, participant2);

            // Act
            await _roomStateService.EnsureControlConsistencyAsync(roomId);

            // Assert
            var controller = await _roomStateService.GetControllerAsync(roomId);
            controller.Should().NotBeNull();
            controller!.Id.Should().Be("user-1");
        }

        [Fact]
        public async Task EnsureControlConsistencyAsync_WithMultipleControllers_ShouldKeepOnlyOldest()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, true);
            await Task.Delay(10);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, true);
            
            await _roomStateService.AddParticipantAsync(roomId, participant1);
            await _roomStateService.AddParticipantAsync(roomId, participant2);

            // Act
            await _roomStateService.EnsureControlConsistencyAsync(roomId);

            // Assert
            var participants = await _roomStateService.GetRoomParticipantsAsync(roomId);
            var controllers = participants.Where(p => p.HasControl).ToList();
            controllers.Should().ContainSingle();
            controllers.First().Id.Should().Be("user-1");
        }

        #endregion

        #region Chat Management Tests

        [Fact]
        public async Task AddMessageAsync_ShouldAddMessageToRoom()
        {
            // Arrange
            var roomId = "room-123";
            var message = new ChatMessage("user-1", "User One", null, "Hello, world!");

            // Act
            await _roomStateService.AddMessageAsync(roomId, message);

            // Assert
            var messages = await _roomStateService.GetRoomMessagesAsync(roomId);
            messages.Should().ContainSingle();
            messages.First().Content.Should().Be("Hello, world!");
        }

        [Fact]
        public async Task AddMessageAsync_ShouldEnforceMaxMessagesLimit()
        {
            // Arrange
            var roomId = "room-123";
            const int maxMessages = 50; // This is the MAX_MESSAGES_PER_ROOM constant

            // Act
            for (int i = 0; i < maxMessages + 10; i++)
            {
                await _roomStateService.AddMessageAsync(roomId, new ChatMessage("user-1", "User", null, $"Message {i}"));
            }

            // Assert
            var messages = await _roomStateService.GetRoomMessagesAsync(roomId);
            messages.Should().HaveCount(maxMessages);
            messages.First().Content.Should().Be("Message 10"); // First 10 should be dequeued
            messages.Last().Content.Should().Be("Message 59");
        }

        [Fact]
        public async Task GetRoomMessagesAsync_FromEmptyRoom_ShouldReturnEmptyList()
        {
            // Act
            var messages = await _roomStateService.GetRoomMessagesAsync("nonexistent");

            // Assert
            messages.Should().BeEmpty();
        }

        #endregion

        #region Participant Count Tests

        [Fact]
        public async Task GetParticipantCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            var roomId = "room-123";
            await _roomStateService.AddParticipantAsync(roomId, new RoomParticipant("user-1", "conn-1", "User One", null));
            await _roomStateService.AddParticipantAsync(roomId, new RoomParticipant("user-2", "conn-2", "User Two", null));
            await _roomStateService.AddParticipantAsync(roomId, new RoomParticipant("user-3", "conn-3", "User Three", null));

            // Act
            var count = await _roomStateService.GetParticipantCountAsync(roomId);

            // Assert
            count.Should().Be(3);
        }

        [Fact]
        public async Task GetParticipantCountAsync_ForEmptyRoom_ShouldReturnZero()
        {
            // Act
            var count = await _roomStateService.GetParticipantCountAsync("nonexistent");

            // Assert
            count.Should().Be(0);
        }

        #endregion

        #region IsParticipantInRoom Tests

        [Fact]
        public async Task IsParticipantInRoomAsync_WithExistingParticipant_ShouldReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            await _roomStateService.AddParticipantAsync(roomId, new RoomParticipant("user-1", "conn-1", "User One", null));

            // Act
            var result = await _roomStateService.IsParticipantInRoomAsync(roomId, "user-1");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsParticipantInRoomAsync_WithNonExistentParticipant_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-123";
            await _roomStateService.AddParticipantAsync(roomId, new RoomParticipant("user-1", "conn-1", "User One", null));

            // Act
            var result = await _roomStateService.IsParticipantInRoomAsync(roomId, "nonexistent");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsParticipantInRoomAsync_InNonExistentRoom_ShouldReturnFalse()
        {
            // Act
            var result = await _roomStateService.IsParticipantInRoomAsync("nonexistent", "user-1");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Room Cleanup Tests

        [Fact]
        public async Task ClearRoomDataAsync_ShouldRemoveAllRoomData()
        {
            // Arrange
            var roomId = "room-123";
            await _roomStateService.AddParticipantAsync(roomId, new RoomParticipant("user-1", "conn-1", "User One", null));
            await _roomStateService.AddMessageAsync(roomId, new ChatMessage("user-1", "User One", null, "Hello"));

            // Act
            await _roomStateService.ClearRoomDataAsync(roomId);

            // Assert
            var participants = await _roomStateService.GetRoomParticipantsAsync(roomId);
            var messages = await _roomStateService.GetRoomMessagesAsync(roomId);
            participants.Should().BeEmpty();
            messages.Should().BeEmpty();
        }

        [Fact]
        public async Task GetActiveRoomIdsAsync_ShouldReturnAllActiveRooms()
        {
            // Arrange
            await _roomStateService.AddParticipantAsync("room-1", new RoomParticipant("user-1", "conn-1", "User One", null));
            await _roomStateService.AddParticipantAsync("room-2", new RoomParticipant("user-2", "conn-2", "User Two", null));
            await _roomStateService.AddParticipantAsync("room-3", new RoomParticipant("user-3", "conn-3", "User Three", null));

            // Act
            var activeRooms = await _roomStateService.GetActiveRoomIdsAsync();

            // Assert
            activeRooms.Should().HaveCount(3);
            activeRooms.Should().Contain(new[] { "room-1", "room-2", "room-3" });
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task ConcurrentAddParticipants_ShouldBeThreadSafe()
        {
            // Arrange
            var roomId = "room-123";

            // Act
            var tasks = Enumerable.Range(0, 100).Select(i =>
            {
                var userId = $"user-{i}";
                return _roomStateService.AddParticipantAsync(roomId, new RoomParticipant(userId, $"conn-{userId}", $"User {userId}", null));
            });

            await Task.WhenAll(tasks);

            // Assert
            var participants = await _roomStateService.GetRoomParticipantsAsync(roomId);
            participants.Should().HaveCount(100);
        }

        [Fact]
        public async Task ConcurrentAddMessages_ShouldBeThreadSafe()
        {
            // Arrange
            var roomId = "room-123";

            // Act
            var tasks = Enumerable.Range(0, 100).Select(messageId =>
            {
                return _roomStateService.AddMessageAsync(roomId, new ChatMessage("user-1", "User", null, $"Message {messageId}"));
            });

            await Task.WhenAll(tasks);

            // Assert
            var messages = await _roomStateService.GetRoomMessagesAsync(roomId);
            messages.Should().HaveCount(50); // Should be limited to MAX_MESSAGES_PER_ROOM
        }

        #endregion
    }
}
