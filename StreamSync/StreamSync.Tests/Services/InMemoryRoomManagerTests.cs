using StreamSync.BusinessLogic.Services.InMemory;
using StreamSync.Models.InMemory;

namespace StreamSync.Tests.Services
{
    public class InMemoryRoomManagerTests
    {
        private readonly InMemoryRoomManager _roomManager;

        public InMemoryRoomManagerTests()
        {
            _roomManager = new InMemoryRoomManager();
        }

        #region AddParticipant Tests

        [Fact]
        public void AddParticipant_ShouldAddParticipantToRoom()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", null);

            // Act
            _roomManager.AddParticipant(roomId, participant);

            // Assert
            var participants = _roomManager.GetRoomParticipants(roomId);
            participants.Should().ContainSingle();
            participants.First().Id.Should().Be("user-1");
        }

        [Fact]
        public void AddParticipant_ShouldAddMultipleParticipants()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null);

            // Act
            _roomManager.AddParticipant(roomId, participant1);
            _roomManager.AddParticipant(roomId, participant2);

            // Assert
            var participants = _roomManager.GetRoomParticipants(roomId);
            participants.Should().HaveCount(2);
        }

        [Fact]
        public void AddParticipant_ShouldUpdateExistingParticipant()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "Old Name", null);
            var updatedParticipant = new RoomParticipant("user-1", "conn-2", "New Name", "avatar.jpg");

            // Act
            _roomManager.AddParticipant(roomId, participant);
            _roomManager.AddParticipant(roomId, updatedParticipant);

            // Assert
            var participants = _roomManager.GetRoomParticipants(roomId);
            participants.Should().ContainSingle();
            participants.First().DisplayName.Should().Be("New Name");
            participants.First().ConnectionId.Should().Be("conn-2");
        }

        #endregion

        #region RemoveParticipant Tests

        [Fact]
        public void RemoveParticipant_ShouldRemoveParticipantFromRoom()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", null);
            _roomManager.AddParticipant(roomId, participant);

            // Act
            _roomManager.RemoveParticipant(roomId, "user-1");

            // Assert
            var participants = _roomManager.GetRoomParticipants(roomId);
            participants.Should().BeEmpty();
        }

        [Fact]
        public void RemoveParticipant_WhenLastParticipant_ShouldCleanupRoom()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", null);
            _roomManager.AddParticipant(roomId, participant);
            _roomManager.AddMessage(roomId, new ChatMessage("user-1", "User One", null, "Hello"));

            // Act
            _roomManager.RemoveParticipant(roomId, "user-1");

            // Assert
            _roomManager.GetRoomParticipants(roomId).Should().BeEmpty();
            _roomManager.GetRoomMessages(roomId).Should().BeEmpty();
        }

        [Fact]
        public void RemoveParticipant_FromNonExistentRoom_ShouldNotThrow()
        {
            // Act
            var action = () => _roomManager.RemoveParticipant("nonexistent", "user-1");

            // Assert
            action.Should().NotThrow();
        }

        #endregion

        #region GetParticipant Tests

        [Fact]
        public void GetParticipant_WithExistingParticipant_ShouldReturnParticipant()
        {
            // Arrange
            var roomId = "room-123";
            var participant = new RoomParticipant("user-1", "conn-1", "User One", "avatar.jpg", true);
            _roomManager.AddParticipant(roomId, participant);

            // Act
            var result = _roomManager.GetParticipant(roomId, "user-1");

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("user-1");
            result.DisplayName.Should().Be("User One");
            result.HasControl.Should().BeTrue();
        }

        [Fact]
        public void GetParticipant_WithNonExistentParticipant_ShouldReturnNull()
        {
            // Arrange
            var roomId = "room-123";
            _roomManager.AddParticipant(roomId, new RoomParticipant("user-1", "conn-1", "User One", null));

            // Act
            var result = _roomManager.GetParticipant(roomId, "nonexistent");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetParticipant_FromNonExistentRoom_ShouldReturnNull()
        {
            // Act
            var result = _roomManager.GetParticipant("nonexistent", "user-1");

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetRoomParticipants Tests

        [Fact]
        public void GetRoomParticipants_ShouldReturnParticipantsOrderedByJoinTime()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null);
            Thread.Sleep(10); // Ensure different join times
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null);
            Thread.Sleep(10);
            var participant3 = new RoomParticipant("user-3", "conn-3", "User Three", null);

            _roomManager.AddParticipant(roomId, participant1);
            _roomManager.AddParticipant(roomId, participant2);
            _roomManager.AddParticipant(roomId, participant3);

            // Act
            var participants = _roomManager.GetRoomParticipants(roomId);

            // Assert
            participants.Should().HaveCount(3);
            participants[0].Id.Should().Be("user-1");
            participants[1].Id.Should().Be("user-2");
            participants[2].Id.Should().Be("user-3");
        }

        [Fact]
        public void GetRoomParticipants_FromEmptyRoom_ShouldReturnEmptyList()
        {
            // Act
            var participants = _roomManager.GetRoomParticipants("nonexistent");

            // Assert
            participants.Should().BeEmpty();
        }

        #endregion

        #region Control Management Tests

        [Fact]
        public void GetController_ShouldReturnParticipantWithControl()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, false);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, true);
            _roomManager.AddParticipant(roomId, participant1);
            _roomManager.AddParticipant(roomId, participant2);

            // Act
            var controller = _roomManager.GetController(roomId);

            // Assert
            controller.Should().NotBeNull();
            controller!.Id.Should().Be("user-2");
        }

        [Fact]
        public void GetController_WithNoController_ShouldReturnNull()
        {
            // Arrange
            var roomId = "room-123";
            _roomManager.AddParticipant(roomId, new RoomParticipant("user-1", "conn-1", "User One", null, false));

            // Act
            var controller = _roomManager.GetController(roomId);

            // Assert
            controller.Should().BeNull();
        }

        [Fact]
        public void SetController_ShouldTransferControl()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, true);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, false);
            _roomManager.AddParticipant(roomId, participant1);
            _roomManager.AddParticipant(roomId, participant2);

            // Act
            _roomManager.SetController(roomId, "user-2");

            // Assert
            var p1 = _roomManager.GetParticipant(roomId, "user-1");
            var p2 = _roomManager.GetParticipant(roomId, "user-2");
            p1!.HasControl.Should().BeFalse();
            p2!.HasControl.Should().BeTrue();
        }

        [Fact]
        public void TransferControlToNext_ShouldGiveControlToOldestParticipant()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, true);
            Thread.Sleep(10);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, false);
            Thread.Sleep(10);
            var participant3 = new RoomParticipant("user-3", "conn-3", "User Three", null, false);
            
            _roomManager.AddParticipant(roomId, participant1);
            _roomManager.AddParticipant(roomId, participant2);
            _roomManager.AddParticipant(roomId, participant3);

            // Act
            _roomManager.TransferControlToNext(roomId, "user-1");

            // Assert
            var newController = _roomManager.GetController(roomId);
            newController.Should().NotBeNull();
            newController!.Id.Should().Be("user-2");
            
            var oldController = _roomManager.GetParticipant(roomId, "user-1");
            oldController!.HasControl.Should().BeFalse();
        }

        [Fact]
        public void EnsureControlConsistency_WithNoController_ShouldAssignToOldest()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, false);
            Thread.Sleep(10);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, false);
            
            _roomManager.AddParticipant(roomId, participant1);
            _roomManager.AddParticipant(roomId, participant2);

            // Act
            _roomManager.EnsureControlConsistency(roomId);

            // Assert
            var controller = _roomManager.GetController(roomId);
            controller.Should().NotBeNull();
            controller!.Id.Should().Be("user-1");
        }

        [Fact]
        public void EnsureControlConsistency_WithMultipleControllers_ShouldKeepOnlyOldest()
        {
            // Arrange
            var roomId = "room-123";
            var participant1 = new RoomParticipant("user-1", "conn-1", "User One", null, true);
            Thread.Sleep(10);
            var participant2 = new RoomParticipant("user-2", "conn-2", "User Two", null, true);
            
            _roomManager.AddParticipant(roomId, participant1);
            _roomManager.AddParticipant(roomId, participant2);

            // Act
            _roomManager.EnsureControlConsistency(roomId);

            // Assert
            var participants = _roomManager.GetRoomParticipants(roomId);
            var controllers = participants.Where(p => p.HasControl).ToList();
            controllers.Should().ContainSingle();
            controllers.First().Id.Should().Be("user-1");
        }

        #endregion

        #region Chat Management Tests

        [Fact]
        public void AddMessage_ShouldAddMessageToRoom()
        {
            // Arrange
            var roomId = "room-123";
            var message = new ChatMessage("user-1", "User One", null, "Hello, world!");

            // Act
            _roomManager.AddMessage(roomId, message);

            // Assert
            var messages = _roomManager.GetRoomMessages(roomId);
            messages.Should().ContainSingle();
            messages.First().Content.Should().Be("Hello, world!");
        }

        [Fact]
        public void AddMessage_ShouldEnforceMaxMessagesLimit()
        {
            // Arrange
            var roomId = "room-123";
            const int maxMessages = 50; // This is the MAX_MESSAGES_PER_ROOM constant

            // Act
            for (int i = 0; i < maxMessages + 10; i++)
            {
                _roomManager.AddMessage(roomId, new ChatMessage("user-1", "User", null, $"Message {i}"));
            }

            // Assert
            var messages = _roomManager.GetRoomMessages(roomId);
            messages.Should().HaveCount(maxMessages);
            messages.First().Content.Should().Be("Message 10"); // First 10 should be dequeued
            messages.Last().Content.Should().Be("Message 59");
        }

        [Fact]
        public void GetRoomMessages_FromEmptyRoom_ShouldReturnEmptyList()
        {
            // Act
            var messages = _roomManager.GetRoomMessages("nonexistent");

            // Assert
            messages.Should().BeEmpty();
        }

        #endregion

        #region Participant Count Tests

        [Fact]
        public void GetParticipantCount_ShouldReturnCorrectCount()
        {
            // Arrange
            var roomId = "room-123";
            _roomManager.AddParticipant(roomId, new RoomParticipant("user-1", "conn-1", "User One", null));
            _roomManager.AddParticipant(roomId, new RoomParticipant("user-2", "conn-2", "User Two", null));
            _roomManager.AddParticipant(roomId, new RoomParticipant("user-3", "conn-3", "User Three", null));

            // Act
            var count = _roomManager.GetParticipantCount(roomId);

            // Assert
            count.Should().Be(3);
        }

        [Fact]
        public void GetParticipantCount_ForEmptyRoom_ShouldReturnZero()
        {
            // Act
            var count = _roomManager.GetParticipantCount("nonexistent");

            // Assert
            count.Should().Be(0);
        }

        #endregion

        #region IsParticipantInRoom Tests

        [Fact]
        public void IsParticipantInRoom_WithExistingParticipant_ShouldReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            _roomManager.AddParticipant(roomId, new RoomParticipant("user-1", "conn-1", "User One", null));

            // Act
            var result = _roomManager.IsParticipantInRoom(roomId, "user-1");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsParticipantInRoom_WithNonExistentParticipant_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-123";
            _roomManager.AddParticipant(roomId, new RoomParticipant("user-1", "conn-1", "User One", null));

            // Act
            var result = _roomManager.IsParticipantInRoom(roomId, "nonexistent");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsParticipantInRoom_InNonExistentRoom_ShouldReturnFalse()
        {
            // Act
            var result = _roomManager.IsParticipantInRoom("nonexistent", "user-1");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Room Cleanup Tests

        [Fact]
        public void ClearRoomData_ShouldRemoveAllRoomData()
        {
            // Arrange
            var roomId = "room-123";
            _roomManager.AddParticipant(roomId, new RoomParticipant("user-1", "conn-1", "User One", null));
            _roomManager.AddMessage(roomId, new ChatMessage("user-1", "User One", null, "Hello"));

            // Act
            _roomManager.ClearRoomData(roomId);

            // Assert
            _roomManager.GetRoomParticipants(roomId).Should().BeEmpty();
            _roomManager.GetRoomMessages(roomId).Should().BeEmpty();
        }

        [Fact]
        public void GetActiveRoomIds_ShouldReturnAllActiveRooms()
        {
            // Arrange
            _roomManager.AddParticipant("room-1", new RoomParticipant("user-1", "conn-1", "User One", null));
            _roomManager.AddParticipant("room-2", new RoomParticipant("user-2", "conn-2", "User Two", null));
            _roomManager.AddParticipant("room-3", new RoomParticipant("user-3", "conn-3", "User Three", null));

            // Act
            var activeRooms = _roomManager.GetActiveRoomIds();

            // Assert
            activeRooms.Should().HaveCount(3);
            activeRooms.Should().Contain(new[] { "room-1", "room-2", "room-3" });
        }

        [Fact]
        public void CleanupEmptyRooms_ShouldRemoveEmptyRooms()
        {
            // Arrange
            _roomManager.AddParticipant("room-1", new RoomParticipant("user-1", "conn-1", "User One", null));
            _roomManager.AddParticipant("room-2", new RoomParticipant("user-2", "conn-2", "User Two", null));
            
            // Make room-1 empty
            _roomManager.RemoveParticipant("room-1", "user-1");

            // Act
            _roomManager.CleanupEmptyRooms();

            // Assert
            var activeRooms = _roomManager.GetActiveRoomIds();
            activeRooms.Should().ContainSingle();
            activeRooms.Should().Contain("room-2");
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public void ConcurrentAddParticipants_ShouldBeThreadSafe()
        {
            // Arrange
            var roomId = "room-123";
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var userId = $"user-{i}";
                tasks.Add(Task.Run(() =>
                {
                    _roomManager.AddParticipant(roomId, new RoomParticipant(userId, $"conn-{userId}", $"User {userId}", null));
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            var participants = _roomManager.GetRoomParticipants(roomId);
            participants.Should().HaveCount(100);
        }

        [Fact]
        public void ConcurrentAddMessages_ShouldBeThreadSafe()
        {
            // Arrange
            var roomId = "room-123";
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var messageId = i;
                tasks.Add(Task.Run(() =>
                {
                    _roomManager.AddMessage(roomId, new ChatMessage("user-1", "User", null, $"Message {messageId}"));
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            var messages = _roomManager.GetRoomMessages(roomId);
            messages.Should().HaveCount(50); // Should be limited to MAX_MESSAGES_PER_ROOM
        }

        #endregion
    }
}
