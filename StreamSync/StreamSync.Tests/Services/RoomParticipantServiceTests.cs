using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using StreamSync.Services;
using StreamSync.Services.Interfaces;
using StreamSync.DTOs;
using StreamSync.Hubs;
using StreamSync.Models;
using StreamSync.Models.InMemory;

namespace StreamSync.Tests.Services
{
    public class RoomParticipantServiceTests
    {
        private readonly Mock<IRoomService> _mockRoomService;
        private readonly Mock<IHubContext<RoomHub, IRoomClient>> _mockHubContext;
        private readonly Mock<IRoomClient> _mockClientProxy;
        private readonly Mock<ILogger<RoomParticipantService>> _mockLogger;
        private readonly Mock<IRoomStateService> _mockRoomStateService;
        private readonly RoomParticipantService _participantService;

        public RoomParticipantServiceTests()
        {
            _mockRoomService = new Mock<IRoomService>();
            _mockHubContext = new Mock<IHubContext<RoomHub, IRoomClient>>();
            _mockClientProxy = new Mock<IRoomClient>();
            _mockLogger = new Mock<ILogger<RoomParticipantService>>();
            _mockRoomStateService = new Mock<IRoomStateService>();

            var mockClients = new Mock<IHubClients<IRoomClient>>();
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            mockClients.Setup(c => c.Client(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

            // Default setup - return empty list
            _mockRoomStateService
                .Setup(r => r.GetRoomParticipantsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<RoomParticipant>());

            _participantService = new RoomParticipantService(
                _mockRoomStateService.Object,
                _mockRoomService.Object,
                _mockHubContext.Object,
                _mockLogger.Object);
        }

        #region GetParticipantDtosAsync Tests

        [Fact]
        public async Task GetParticipantDtosAsync_WithParticipants_ShouldReturnMappedDtos()
        {
            // Arrange
            var roomId = "room-1";
            var adminId = "admin-1";
            var room = new Room
            {
                Id = roomId,
                Name = "Test Room",
                VideoUrl = "https://youtube.com/watch?v=test",
                AdminId = adminId
            };

            var participants = new List<RoomParticipant>
            {
                new RoomParticipant(adminId, "conn-1", "Admin", "https://avatar.com/admin.png", true),
                new RoomParticipant("user-2", "conn-2", "User2", "https://avatar.com/user2.png", false)
            };
            
            _mockRoomStateService
                .Setup(r => r.GetRoomParticipantsAsync(roomId))
                .ReturnsAsync(participants);
            
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync(room);

            // Act
            var result = await _participantService.GetParticipantDtosAsync(roomId);

            // Assert
            result.Should().HaveCount(2);
            
            var adminDto = result.First(p => p.Id == adminId);
            adminDto.IsAdmin.Should().BeTrue();
            adminDto.HasControl.Should().BeTrue();
            adminDto.DisplayName.Should().Be("Admin");

            var userDto = result.First(p => p.Id == "user-2");
            userDto.IsAdmin.Should().BeFalse();
            userDto.HasControl.Should().BeFalse();
        }

        [Fact]
        public async Task GetParticipantDtosAsync_WithNoParticipants_ShouldReturnEmptyList()
        {
            // Arrange
            var roomId = "room-1";
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync((Room?)null);

            // Act
            var result = await _participantService.GetParticipantDtosAsync(roomId);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetParticipantDtosAsync_WithNullRoom_ShouldStillReturnDtos()
        {
            // Arrange
            var roomId = "room-1";
            var participants = new List<RoomParticipant>
            {
                new RoomParticipant("user-1", "conn-1", "User1", null, false)
            };
            
            _mockRoomStateService
                .Setup(r => r.GetRoomParticipantsAsync(roomId))
                .ReturnsAsync(participants);
            
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync((Room?)null);

            // Act
            var result = await _participantService.GetParticipantDtosAsync(roomId);

            // Assert
            result.Should().HaveCount(1);
            result.First().IsAdmin.Should().BeFalse(); // No adminId to compare
        }

        #endregion

        #region MapToDto Tests

        [Fact]
        public async Task MapToDto_WithAdminParticipant_ShouldSetIsAdminTrue()
        {
            // Arrange
            var roomId = "room-1";
            var adminId = "admin-1";
            var room = new Room
            {
                Id = roomId,
                Name = "Test Room",
                VideoUrl = "https://youtube.com/watch?v=test",
                AdminId = adminId
            };
            
            var participant = new RoomParticipant(adminId, "conn-1", "Admin", "https://avatar.com/admin.png", true);
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync(room);

            // Act
            var result = await _participantService.MapToDto(roomId, participant);

            // Assert
            result.IsAdmin.Should().BeTrue();
            result.HasControl.Should().BeTrue();
            result.Id.Should().Be(adminId);
            result.DisplayName.Should().Be("Admin");
            result.AvatarUrl.Should().Be("https://avatar.com/admin.png");
        }

        [Fact]
        public async Task MapToDto_WithNonAdminParticipant_ShouldSetIsAdminFalse()
        {
            // Arrange
            var roomId = "room-1";
            var room = new Room
            {
                Id = roomId,
                Name = "Test Room",
                VideoUrl = "https://youtube.com/watch?v=test",
                AdminId = "admin-1"
            };
            
            var participant = new RoomParticipant("user-2", "conn-2", "User2", null, false);
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync(room);

            // Act
            var result = await _participantService.MapToDto(roomId, participant);

            // Assert
            result.IsAdmin.Should().BeFalse();
            result.HasControl.Should().BeFalse();
            result.Id.Should().Be("user-2");
            result.DisplayName.Should().Be("User2");
            result.AvatarUrl.Should().BeNull();
        }

        #endregion

        #region BroadcastParticipantsAsync Tests

        [Fact]
        public async Task BroadcastParticipantsAsync_ShouldSendParticipantsToGroup()
        {
            // Arrange
            var roomId = "room-1";
            var room = new Room
            {
                Id = roomId,
                Name = "Test Room",
                VideoUrl = "https://youtube.com/watch?v=test",
                AdminId = "admin-1"
            };

            var participants = new List<RoomParticipant>
            {
                new RoomParticipant("admin-1", "conn-1", "Admin", null, true),
                new RoomParticipant("user-2", "conn-2", "User2", null, false)
            };
            
            _mockRoomStateService
                .Setup(r => r.GetRoomParticipantsAsync(roomId))
                .ReturnsAsync(participants);
            
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync(room);

            // Act
            await _participantService.BroadcastParticipantsAsync(roomId);

            // Assert
            _mockClientProxy.Verify(
                c => c.ReceiveRoomParticipants(It.Is<List<RoomParticipantDto>>(p => p.Count == 2)),
                Times.Once);
        }

        [Fact]
        public async Task BroadcastParticipantsAsync_WhenBroadcastFails_ShouldNotThrow()
        {
            // Arrange
            var roomId = "room-1";
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync((Room?)null);
            
            _mockClientProxy
                .Setup(c => c.ReceiveRoomParticipants(It.IsAny<List<RoomParticipantDto>>()))
                .ThrowsAsync(new Exception("Broadcast failed"));

            // Act & Assert - Should not throw
            await _participantService.Invoking(s => s.BroadcastParticipantsAsync(roomId))
                .Should().NotThrowAsync();
        }

        #endregion

        #region SendParticipantsToClientAsync Tests

        [Fact]
        public async Task SendParticipantsToClientAsync_ShouldSendToSpecificClient()
        {
            // Arrange
            var connectionId = "conn-123";
            var roomId = "room-1";
            var room = new Room
            {
                Id = roomId,
                Name = "Test Room",
                VideoUrl = "https://youtube.com/watch?v=test",
                AdminId = "admin-1"
            };

            var participants = new List<RoomParticipant>
            {
                new RoomParticipant("admin-1", "conn-1", "Admin", null, true)
            };
            
            _mockRoomStateService
                .Setup(r => r.GetRoomParticipantsAsync(roomId))
                .ReturnsAsync(participants);
            
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync(room);

            // Act
            await _participantService.SendParticipantsToClientAsync(connectionId, roomId);

            // Assert
            _mockClientProxy.Verify(
                c => c.ReceiveRoomParticipants(It.Is<List<RoomParticipantDto>>(p => p.Count == 1)),
                Times.Once);
        }

        [Fact]
        public async Task SendParticipantsToClientAsync_WhenSendFails_ShouldNotThrow()
        {
            // Arrange
            var connectionId = "conn-123";
            var roomId = "room-1";
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync((Room?)null);
            
            _mockClientProxy
                .Setup(c => c.ReceiveRoomParticipants(It.IsAny<List<RoomParticipantDto>>()))
                .ThrowsAsync(new Exception("Send failed"));

            // Act & Assert - Should not throw
            await _participantService.Invoking(s => s.SendParticipantsToClientAsync(connectionId, roomId))
                .Should().NotThrowAsync();
        }

        #endregion

        #region NotifyParticipantJoinedAsync Tests

        [Fact]
        public async Task NotifyParticipantJoinedAsync_ShouldSendBothNotifications()
        {
            // Arrange
            var roomId = "room-1";
            var participantId = "user-1";
            var displayName = "TestUser";
            var avatarUrl = "https://avatar.com/user.png";

            // Act
            await _participantService.NotifyParticipantJoinedAsync(roomId, participantId, displayName, avatarUrl);

            // Assert
            _mockClientProxy.Verify(c => c.RoomJoined(roomId, participantId, displayName, avatarUrl), Times.Once);
            _mockClientProxy.Verify(c => c.ParticipantJoinedNotification(displayName), Times.Once);
        }

        [Fact]
        public async Task NotifyParticipantJoinedAsync_WhenNotificationFails_ShouldNotThrow()
        {
            // Arrange
            var roomId = "room-1";
            var participantId = "user-1";
            var displayName = "TestUser";
            var avatarUrl = "https://avatar.com/user.png";

            _mockClientProxy
                .Setup(c => c.RoomJoined(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Notification failed"));

            // Act & Assert - Should not throw
            await _participantService.Invoking(s => s.NotifyParticipantJoinedAsync(roomId, participantId, displayName, avatarUrl))
                .Should().NotThrowAsync();
        }

        #endregion

        #region NotifyParticipantLeftAsync Tests

        [Fact]
        public async Task NotifyParticipantLeftAsync_ShouldSendBothNotifications()
        {
            // Arrange
            var roomId = "room-1";
            var participantId = "user-1";
            var displayName = "TestUser";

            // Act
            await _participantService.NotifyParticipantLeftAsync(roomId, participantId, displayName);

            // Assert
            _mockClientProxy.Verify(c => c.RoomLeft(roomId, participantId, displayName), Times.Once);
            _mockClientProxy.Verify(c => c.ParticipantLeftNotification(displayName), Times.Once);
        }

        [Fact]
        public async Task NotifyParticipantLeftAsync_WhenNotificationFails_ShouldNotThrow()
        {
            // Arrange
            var roomId = "room-1";
            var participantId = "user-1";
            var displayName = "TestUser";

            _mockClientProxy
                .Setup(c => c.RoomLeft(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Notification failed"));

            // Act & Assert - Should not throw
            await _participantService.Invoking(s => s.NotifyParticipantLeftAsync(roomId, participantId, displayName))
                .Should().NotThrowAsync();
        }

        #endregion

        #region NotifyControlTransferredAsync Tests

        [Fact]
        public async Task NotifyControlTransferredAsync_ShouldSendNotification()
        {
            // Arrange
            var roomId = "room-1";
            var newControllerId = "user-2";
            var newControllerName = "User2";

            // Act
            await _participantService.NotifyControlTransferredAsync(roomId, newControllerId, newControllerName);

            // Assert
            _mockClientProxy.Verify(c => c.ControlTransferred(newControllerId, newControllerName), Times.Once);
        }

        [Fact]
        public async Task NotifyControlTransferredAsync_WhenNotificationFails_ShouldNotThrow()
        {
            // Arrange
            var roomId = "room-1";
            var newControllerId = "user-2";
            var newControllerName = "User2";

            _mockClientProxy
                .Setup(c => c.ControlTransferred(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Notification failed"));

            // Act & Assert - Should not throw
            await _participantService.Invoking(s => s.NotifyControlTransferredAsync(roomId, newControllerId, newControllerName))
                .Should().NotThrowAsync();
        }

        #endregion
    }
}
