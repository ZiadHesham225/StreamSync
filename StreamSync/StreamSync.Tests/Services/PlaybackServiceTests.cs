using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using StreamSync.Services;
using StreamSync.Services.Interfaces;
using StreamSync.Hubs;
using StreamSync.Models;

namespace StreamSync.Tests.Services
{
    public class PlaybackServiceTests
    {
        private readonly Mock<IRoomService> _mockRoomService;
        private readonly Mock<IHubContext<RoomHub, IRoomClient>> _mockHubContext;
        private readonly Mock<IRoomClient> _mockClientProxy;
        private readonly Mock<IRoomClient> _mockGroupExceptProxy;
        private readonly Mock<ILogger<PlaybackService>> _mockLogger;
        private readonly PlaybackService _playbackService;

        public PlaybackServiceTests()
        {
            _mockRoomService = new Mock<IRoomService>();
            _mockHubContext = new Mock<IHubContext<RoomHub, IRoomClient>>();
            _mockClientProxy = new Mock<IRoomClient>();
            _mockGroupExceptProxy = new Mock<IRoomClient>();
            _mockLogger = new Mock<ILogger<PlaybackService>>();

            var mockClients = new Mock<IHubClients<IRoomClient>>();
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            mockClients.Setup(c => c.Client(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            mockClients.Setup(c => c.GroupExcept(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                .Returns(_mockGroupExceptProxy.Object);
            _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

            _playbackService = new PlaybackService(
                _mockRoomService.Object,
                _mockHubContext.Object,
                _mockLogger.Object);
        }

        #region UpdatePlaybackAsync Tests

        [Fact]
        public async Task UpdatePlaybackAsync_WithValidData_ShouldUpdateAndBroadcast()
        {
            // Arrange
            var roomId = "room-1";
            var userId = "user-1";
            var position = 120.5;
            var isPlaying = true;

            _mockRoomService
                .Setup(s => s.UpdatePlaybackStateAsync(roomId, userId, position, isPlaying))
                .ReturnsAsync(true);

            // Act
            var result = await _playbackService.UpdatePlaybackAsync(roomId, userId, position, isPlaying);

            // Assert
            result.Should().BeTrue();
            _mockClientProxy.Verify(c => c.ReceivePlaybackUpdate(position, isPlaying), Times.Once);
        }

        [Fact]
        public async Task UpdatePlaybackAsync_WhenRoomServiceReturnsFalse_ShouldNotBroadcast()
        {
            // Arrange
            var roomId = "room-1";
            var userId = "user-1";
            var position = 120.5;
            var isPlaying = true;

            _mockRoomService
                .Setup(s => s.UpdatePlaybackStateAsync(roomId, userId, position, isPlaying))
                .ReturnsAsync(false);

            // Act
            var result = await _playbackService.UpdatePlaybackAsync(roomId, userId, position, isPlaying);

            // Assert
            result.Should().BeFalse();
            _mockClientProxy.Verify(c => c.ReceivePlaybackUpdate(It.IsAny<double>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task UpdatePlaybackAsync_WithPausedState_ShouldBroadcastCorrectly()
        {
            // Arrange
            var roomId = "room-1";
            var userId = "user-1";
            var position = 60.0;
            var isPlaying = false;

            _mockRoomService
                .Setup(s => s.UpdatePlaybackStateAsync(roomId, userId, position, isPlaying))
                .ReturnsAsync(true);

            // Act
            var result = await _playbackService.UpdatePlaybackAsync(roomId, userId, position, isPlaying);

            // Assert
            result.Should().BeTrue();
            _mockClientProxy.Verify(c => c.ReceivePlaybackUpdate(60.0, false), Times.Once);
        }

        [Fact]
        public async Task UpdatePlaybackAsync_WhenBroadcastFails_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-1";
            var userId = "user-1";
            var position = 120.5;
            var isPlaying = true;

            _mockRoomService
                .Setup(s => s.UpdatePlaybackStateAsync(roomId, userId, position, isPlaying))
                .ReturnsAsync(true);

            _mockClientProxy
                .Setup(c => c.ReceivePlaybackUpdate(It.IsAny<double>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception("Broadcast failed"));

            // Act
            var result = await _playbackService.UpdatePlaybackAsync(roomId, userId, position, isPlaying);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region BroadcastHeartbeatAsync Tests

        [Fact]
        public async Task BroadcastHeartbeatAsync_ShouldSendToGroupExceptSender()
        {
            // Arrange
            var roomId = "room-1";
            var senderConnectionId = "conn-123";
            var position = 150.0;

            // Act
            await _playbackService.BroadcastHeartbeatAsync(roomId, senderConnectionId, position);

            // Assert
            _mockGroupExceptProxy.Verify(c => c.ReceiveHeartbeat(position), Times.Once);
        }

        [Fact]
        public async Task BroadcastHeartbeatAsync_WithZeroPosition_ShouldStillBroadcast()
        {
            // Arrange
            var roomId = "room-1";
            var senderConnectionId = "conn-123";
            var position = 0.0;

            // Act
            await _playbackService.BroadcastHeartbeatAsync(roomId, senderConnectionId, position);

            // Assert
            _mockGroupExceptProxy.Verify(c => c.ReceiveHeartbeat(0.0), Times.Once);
        }

        [Fact]
        public async Task BroadcastHeartbeatAsync_WhenBroadcastFails_ShouldNotThrow()
        {
            // Arrange
            var roomId = "room-1";
            var senderConnectionId = "conn-123";
            var position = 150.0;

            _mockGroupExceptProxy
                .Setup(c => c.ReceiveHeartbeat(It.IsAny<double>()))
                .ThrowsAsync(new Exception("Broadcast failed"));

            // Act & Assert - Should not throw
            await _playbackService.Invoking(s => s.BroadcastHeartbeatAsync(roomId, senderConnectionId, position))
                .Should().NotThrowAsync();
        }

        #endregion

        #region ForceSyncAsync Tests

        [Fact]
        public async Task ForceSyncAsync_ShouldBroadcastToEntireGroup()
        {
            // Arrange
            var roomId = "room-1";
            var position = 200.0;
            var isPlaying = true;

            // Act
            await _playbackService.ForceSyncAsync(roomId, position, isPlaying);

            // Assert
            _mockClientProxy.Verify(c => c.ForceSyncPlayback(position, isPlaying), Times.Once);
        }

        [Fact]
        public async Task ForceSyncAsync_WithPausedState_ShouldBroadcastCorrectly()
        {
            // Arrange
            var roomId = "room-1";
            var position = 100.0;
            var isPlaying = false;

            // Act
            await _playbackService.ForceSyncAsync(roomId, position, isPlaying);

            // Assert
            _mockClientProxy.Verify(c => c.ForceSyncPlayback(100.0, false), Times.Once);
        }

        [Fact]
        public async Task ForceSyncAsync_WhenBroadcastFails_ShouldNotThrow()
        {
            // Arrange
            var roomId = "room-1";
            var position = 200.0;
            var isPlaying = true;

            _mockClientProxy
                .Setup(c => c.ForceSyncPlayback(It.IsAny<double>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception("Broadcast failed"));

            // Act & Assert - Should not throw
            await _playbackService.Invoking(s => s.ForceSyncAsync(roomId, position, isPlaying))
                .Should().NotThrowAsync();
        }

        #endregion

        #region SendPlaybackStateToClientAsync Tests

        [Fact]
        public async Task SendPlaybackStateToClientAsync_WithActiveRoom_ShouldSendState()
        {
            // Arrange
            var connectionId = "conn-123";
            var roomId = "room-1";
            var room = new Room
            {
                Id = roomId,
                Name = "Test Room",
                VideoUrl = "https://youtube.com/watch?v=test",
                AdminId = "admin-1",
                IsActive = true,
                CurrentPosition = 150.0,
                IsPlaying = true
            };

            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync(room);

            // Act
            await _playbackService.SendPlaybackStateToClientAsync(connectionId, roomId);

            // Assert
            _mockClientProxy.Verify(c => c.ForceSyncPlayback(150.0, true), Times.Once);
        }

        [Fact]
        public async Task SendPlaybackStateToClientAsync_WithInactiveRoom_ShouldNotSendState()
        {
            // Arrange
            var connectionId = "conn-123";
            var roomId = "room-1";
            var room = new Room
            {
                Id = roomId,
                Name = "Test Room",
                VideoUrl = "https://youtube.com/watch?v=test",
                AdminId = "admin-1",
                IsActive = false,
                CurrentPosition = 150.0,
                IsPlaying = true
            };

            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync(room);

            // Act
            await _playbackService.SendPlaybackStateToClientAsync(connectionId, roomId);

            // Assert
            _mockClientProxy.Verify(c => c.ForceSyncPlayback(It.IsAny<double>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task SendPlaybackStateToClientAsync_WithNullRoom_ShouldNotSendState()
        {
            // Arrange
            var connectionId = "conn-123";
            var roomId = "room-1";

            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync((Room?)null);

            // Act
            await _playbackService.SendPlaybackStateToClientAsync(connectionId, roomId);

            // Assert
            _mockClientProxy.Verify(c => c.ForceSyncPlayback(It.IsAny<double>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task SendPlaybackStateToClientAsync_WhenSendFails_ShouldNotThrow()
        {
            // Arrange
            var connectionId = "conn-123";
            var roomId = "room-1";
            var room = new Room
            {
                Id = roomId,
                Name = "Test Room",
                VideoUrl = "https://youtube.com/watch?v=test",
                AdminId = "admin-1",
                IsActive = true,
                CurrentPosition = 150.0,
                IsPlaying = true
            };

            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId)).ReturnsAsync(room);
            _mockClientProxy
                .Setup(c => c.ForceSyncPlayback(It.IsAny<double>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception("Send failed"));

            // Act & Assert - Should not throw
            await _playbackService.Invoking(s => s.SendPlaybackStateToClientAsync(connectionId, roomId))
                .Should().NotThrowAsync();
        }

        #endregion

        #region NotifyVideoChangedAsync Tests

        [Fact]
        public async Task NotifyVideoChangedAsync_ShouldBroadcastVideoChange()
        {
            // Arrange
            var roomId = "room-1";
            var videoUrl = "https://youtube.com/watch?v=newvideo";
            var videoTitle = "New Video Title";
            var videoThumbnail = "https://thumbnail.com/image.jpg";

            // Act
            await _playbackService.NotifyVideoChangedAsync(roomId, videoUrl, videoTitle, videoThumbnail);

            // Assert
            _mockClientProxy.Verify(c => c.VideoChanged(videoUrl, videoTitle, videoThumbnail), Times.Once);
        }

        [Fact]
        public async Task NotifyVideoChangedAsync_WithNullThumbnail_ShouldStillBroadcast()
        {
            // Arrange
            var roomId = "room-1";
            var videoUrl = "https://youtube.com/watch?v=newvideo";
            var videoTitle = "New Video Title";

            // Act
            await _playbackService.NotifyVideoChangedAsync(roomId, videoUrl, videoTitle, null);

            // Assert
            _mockClientProxy.Verify(c => c.VideoChanged(videoUrl, videoTitle, null), Times.Once);
        }

        [Fact]
        public async Task NotifyVideoChangedAsync_WhenBroadcastFails_ShouldNotThrow()
        {
            // Arrange
            var roomId = "room-1";
            var videoUrl = "https://youtube.com/watch?v=newvideo";
            var videoTitle = "New Video Title";

            _mockClientProxy
                .Setup(c => c.VideoChanged(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ThrowsAsync(new Exception("Broadcast failed"));

            // Act & Assert - Should not throw
            await _playbackService.Invoking(s => s.NotifyVideoChangedAsync(roomId, videoUrl, videoTitle, null))
                .Should().NotThrowAsync();
        }

        #endregion
    }
}
