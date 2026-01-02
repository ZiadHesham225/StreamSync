using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using StreamSync.Services;
using StreamSync.Services.InMemory;
using StreamSync.DTOs;
using StreamSync.Hubs;
using StreamSync.Models.InMemory;

namespace StreamSync.Tests.Services
{
    public class VirtualBrowserNotificationServiceTests
    {
        private readonly Mock<IHubContext<RoomHub, IRoomClient>> _mockHubContext;
        private readonly Mock<IRoomClient> _mockClientProxy;
        private readonly Mock<ILogger<VirtualBrowserNotificationService>> _mockLogger;
        private readonly InMemoryRoomManager _roomManager;
        private readonly VirtualBrowserNotificationService _notificationService;

        public VirtualBrowserNotificationServiceTests()
        {
            _mockHubContext = new Mock<IHubContext<RoomHub, IRoomClient>>();
            _mockClientProxy = new Mock<IRoomClient>();
            _mockLogger = new Mock<ILogger<VirtualBrowserNotificationService>>();
            _roomManager = new InMemoryRoomManager();

            var mockClients = new Mock<IHubClients<IRoomClient>>();
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            mockClients.Setup(c => c.Client(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

            _notificationService = new VirtualBrowserNotificationService(
                _mockHubContext.Object,
                _roomManager,
                _mockLogger.Object);
        }

        #region NotifyBrowserAllocatedAsync Tests

        [Fact]
        public async Task NotifyBrowserAllocatedAsync_WithValidData_ShouldNotifyGroup()
        {
            // Arrange
            var roomId = "room-1";
            var browser = new VirtualBrowserDto
            {
                Id = "browser-1",
                RoomId = roomId,
                ContainerId = "container-123",
                ContainerName = "neko-room-1",
                BrowserUrl = "https://localhost:8080",
                WebRtcUrl = "wss://localhost:52000",
                HttpPort = 8080,
                Status = "allocated"
            };

            // Act
            await _notificationService.NotifyBrowserAllocatedAsync(roomId, browser);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserAllocated(browser), Times.Once);
        }

        [Fact]
        public async Task NotifyBrowserAllocatedAsync_WithEmptyRoomId_ShouldNotNotify()
        {
            // Arrange
            var browser = new VirtualBrowserDto
            {
                Id = "browser-1",
                RoomId = "",
                Status = "allocated"
            };

            // Act
            await _notificationService.NotifyBrowserAllocatedAsync("", browser);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserAllocated(It.IsAny<VirtualBrowserDto>()), Times.Never);
        }

        [Fact]
        public async Task NotifyBrowserAllocatedAsync_WithNullBrowser_ShouldNotNotify()
        {
            // Arrange
            var roomId = "room-1";

            // Act
            await _notificationService.NotifyBrowserAllocatedAsync(roomId, null!);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserAllocated(It.IsAny<VirtualBrowserDto>()), Times.Never);
        }

        #endregion

        #region NotifyBrowserReleasedAsync Tests

        [Fact]
        public async Task NotifyBrowserReleasedAsync_WithValidRoomId_ShouldNotifyGroup()
        {
            // Arrange
            var roomId = "room-1";

            // Act
            await _notificationService.NotifyBrowserReleasedAsync(roomId);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserReleased(), Times.Once);
        }

        [Fact]
        public async Task NotifyBrowserReleasedAsync_WithEmptyRoomId_ShouldNotNotify()
        {
            // Act
            await _notificationService.NotifyBrowserReleasedAsync("");

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserReleased(), Times.Never);
        }

        [Fact]
        public async Task NotifyBrowserReleasedAsync_WithNullRoomId_ShouldNotNotify()
        {
            // Act
            await _notificationService.NotifyBrowserReleasedAsync(null!);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserReleased(), Times.Never);
        }

        #endregion

        #region NotifyBrowserExpiredAsync Tests

        [Fact]
        public async Task NotifyBrowserExpiredAsync_WithValidRoomId_ShouldNotifyGroup()
        {
            // Arrange
            var roomId = "room-1";

            // Act
            await _notificationService.NotifyBrowserExpiredAsync(roomId);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserExpired(), Times.Once);
        }

        [Fact]
        public async Task NotifyBrowserExpiredAsync_WithEmptyRoomId_ShouldNotNotify()
        {
            // Act
            await _notificationService.NotifyBrowserExpiredAsync("");

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserExpired(), Times.Never);
        }

        #endregion

        #region NotifyQueuedAsync Tests

        [Fact]
        public async Task NotifyQueuedAsync_WithValidData_ShouldNotifyGroup()
        {
            // Arrange
            var roomId = "room-1";
            var queueStatus = new VirtualBrowserQueueDto
            {
                Id = "queue-1",
                RoomId = roomId,
                Position = 3,
                Status = "waiting",
                RequestedAt = DateTime.UtcNow
            };

            // Act
            await _notificationService.NotifyQueuedAsync(roomId, queueStatus);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserQueued(queueStatus), Times.Once);
        }

        [Fact]
        public async Task NotifyQueuedAsync_WithEmptyRoomId_ShouldNotNotify()
        {
            // Arrange
            var queueStatus = new VirtualBrowserQueueDto
            {
                Id = "queue-1",
                Position = 1,
                Status = "waiting"
            };

            // Act
            await _notificationService.NotifyQueuedAsync("", queueStatus);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserQueued(It.IsAny<VirtualBrowserQueueDto>()), Times.Never);
        }

        [Fact]
        public async Task NotifyQueuedAsync_WithNullQueueStatus_ShouldNotNotify()
        {
            // Arrange
            var roomId = "room-1";

            // Act
            await _notificationService.NotifyQueuedAsync(roomId, null!);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserQueued(It.IsAny<VirtualBrowserQueueDto>()), Times.Never);
        }

        #endregion

        #region NotifyQueueCancelledAsync Tests

        [Fact]
        public async Task NotifyQueueCancelledAsync_WithValidRoomId_ShouldNotifyGroup()
        {
            // Arrange
            var roomId = "room-1";

            // Act
            await _notificationService.NotifyQueueCancelledAsync(roomId);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserQueueCancelled(), Times.Once);
        }

        [Fact]
        public async Task NotifyQueueCancelledAsync_WithEmptyRoomId_ShouldNotNotify()
        {
            // Act
            await _notificationService.NotifyQueueCancelledAsync("");

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserQueueCancelled(), Times.Never);
        }

        #endregion

        #region NotifyBrowserAvailableAsync Tests

        [Fact]
        public async Task NotifyBrowserAvailableAsync_WithController_ShouldNotifyController()
        {
            // Arrange
            var roomId = "room-1";
            var controllerId = "user-1";
            var controllerConnectionId = "conn-123";
            var queueStatus = new VirtualBrowserQueueDto
            {
                Id = "queue-1",
                RoomId = roomId,
                Position = 0,
                Status = "available"
            };

            var participant = new RoomParticipant(controllerId, controllerConnectionId, "Controller", null, true);
            _roomManager.AddParticipant(roomId, participant);

            // Act
            await _notificationService.NotifyBrowserAvailableAsync(roomId, queueStatus);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserAvailable(queueStatus), Times.Once);
        }

        [Fact]
        public async Task NotifyBrowserAvailableAsync_WithNoController_ShouldNotifyWholeGroup()
        {
            // Arrange
            var roomId = "room-1";
            var queueStatus = new VirtualBrowserQueueDto
            {
                Id = "queue-1",
                RoomId = roomId,
                Position = 0,
                Status = "available"
            };

            // No participants with control

            // Act
            await _notificationService.NotifyBrowserAvailableAsync(roomId, queueStatus);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserAvailable(queueStatus), Times.Once);
        }

        [Fact]
        public async Task NotifyBrowserAvailableAsync_WithEmptyRoomId_ShouldNotNotify()
        {
            // Arrange
            var queueStatus = new VirtualBrowserQueueDto
            {
                Id = "queue-1",
                Position = 0,
                Status = "available"
            };

            // Act
            await _notificationService.NotifyBrowserAvailableAsync("", queueStatus);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserAvailable(It.IsAny<VirtualBrowserQueueDto>()), Times.Never);
        }

        [Fact]
        public async Task NotifyBrowserAvailableAsync_WithNullQueueStatus_ShouldNotNotify()
        {
            // Arrange
            var roomId = "room-1";

            // Act
            await _notificationService.NotifyBrowserAvailableAsync(roomId, null!);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserAvailable(It.IsAny<VirtualBrowserQueueDto>()), Times.Never);
        }

        #endregion

        #region NotifyQueueNotificationExpiredAsync Tests

        [Fact]
        public async Task NotifyQueueNotificationExpiredAsync_WithValidRoomId_ShouldNotifyGroup()
        {
            // Arrange
            var roomId = "room-1";

            // Act
            await _notificationService.NotifyQueueNotificationExpiredAsync(roomId);

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserQueueNotificationExpired(), Times.Once);
        }

        [Fact]
        public async Task NotifyQueueNotificationExpiredAsync_WithEmptyRoomId_ShouldNotNotify()
        {
            // Act
            await _notificationService.NotifyQueueNotificationExpiredAsync("");

            // Assert
            _mockClientProxy.Verify(c => c.VirtualBrowserQueueNotificationExpired(), Times.Never);
        }

        #endregion

        #region NotifyVideoChangedAsync Tests

        [Fact]
        public async Task NotifyVideoChangedAsync_WithValidData_ShouldNotifyGroup()
        {
            // Arrange
            var roomId = "room-1";
            var videoUrl = "https://neko.localhost:8080";
            var videoTitle = "Virtual Browser Session";
            var videoThumbnail = "https://thumbnail.com/neko.png";

            // Act
            await _notificationService.NotifyVideoChangedAsync(roomId, videoUrl, videoTitle, videoThumbnail);

            // Assert
            _mockClientProxy.Verify(c => c.VideoChanged(videoUrl, videoTitle, videoThumbnail), Times.Once);
        }

        [Fact]
        public async Task NotifyVideoChangedAsync_WithNullThumbnail_ShouldStillNotify()
        {
            // Arrange
            var roomId = "room-1";
            var videoUrl = "https://neko.localhost:8080";
            var videoTitle = "Virtual Browser Session";

            // Act
            await _notificationService.NotifyVideoChangedAsync(roomId, videoUrl, videoTitle, null);

            // Assert
            _mockClientProxy.Verify(c => c.VideoChanged(videoUrl, videoTitle, null), Times.Once);
        }

        [Fact]
        public async Task NotifyVideoChangedAsync_WithEmptyRoomId_ShouldNotNotify()
        {
            // Arrange
            var videoUrl = "https://neko.localhost:8080";
            var videoTitle = "Virtual Browser Session";

            // Act
            await _notificationService.NotifyVideoChangedAsync("", videoUrl, videoTitle, null);

            // Assert
            _mockClientProxy.Verify(c => c.VideoChanged(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task NotifyBrowserAllocatedAsync_WhenHubThrows_ShouldPropagateException()
        {
            // Arrange
            var roomId = "room-1";
            var browser = new VirtualBrowserDto
            {
                Id = "browser-1",
                RoomId = roomId,
                Status = "allocated"
            };

            _mockClientProxy
                .Setup(c => c.VirtualBrowserAllocated(It.IsAny<VirtualBrowserDto>()))
                .ThrowsAsync(new Exception("Hub error"));

            // Act & Assert - Exception should propagate since this service doesn't catch exceptions
            await _notificationService.Invoking(s => s.NotifyBrowserAllocatedAsync(roomId, browser))
                .Should().ThrowAsync<Exception>()
                .WithMessage("Hub error");
        }

        [Fact]
        public async Task NotifyBrowserReleasedAsync_WhenHubThrows_ShouldPropagateException()
        {
            // Arrange
            var roomId = "room-1";

            _mockClientProxy
                .Setup(c => c.VirtualBrowserReleased())
                .ThrowsAsync(new Exception("Hub error"));

            // Act & Assert
            await _notificationService.Invoking(s => s.NotifyBrowserReleasedAsync(roomId))
                .Should().ThrowAsync<Exception>()
                .WithMessage("Hub error");
        }

        [Fact]
        public async Task NotifyBrowserExpiredAsync_WhenHubThrows_ShouldPropagateException()
        {
            // Arrange
            var roomId = "room-1";

            _mockClientProxy
                .Setup(c => c.VirtualBrowserExpired())
                .ThrowsAsync(new Exception("Hub error"));

            // Act & Assert
            await _notificationService.Invoking(s => s.NotifyBrowserExpiredAsync(roomId))
                .Should().ThrowAsync<Exception>()
                .WithMessage("Hub error");
        }

        [Fact]
        public async Task NotifyQueuedAsync_WhenHubThrows_ShouldPropagateException()
        {
            // Arrange
            var roomId = "room-1";
            var queueStatus = new VirtualBrowserQueueDto
            {
                Id = "queue-1",
                Position = 1,
                Status = "waiting"
            };

            _mockClientProxy
                .Setup(c => c.VirtualBrowserQueued(It.IsAny<VirtualBrowserQueueDto>()))
                .ThrowsAsync(new Exception("Hub error"));

            // Act & Assert
            await _notificationService.Invoking(s => s.NotifyQueuedAsync(roomId, queueStatus))
                .Should().ThrowAsync<Exception>()
                .WithMessage("Hub error");
        }

        #endregion
    }
}
