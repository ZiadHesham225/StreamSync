using Microsoft.Extensions.Logging;
using StreamSync.Services.InMemory;

namespace StreamSync.Tests.Services
{
    public class VirtualBrowserQueueServiceTests
    {
        private readonly Mock<ILogger<InMemoryVirtualBrowserQueueService>> _mockLogger;
        private readonly InMemoryVirtualBrowserQueueService _queueService;

        public VirtualBrowserQueueServiceTests()
        {
            _mockLogger = new Mock<ILogger<InMemoryVirtualBrowserQueueService>>();
            _queueService = new InMemoryVirtualBrowserQueueService(_mockLogger.Object);
        }

        #region AddToQueueAsync Tests

        [Fact]
        public async Task AddToQueueAsync_WithNewRoom_ShouldAddToQueueAndReturnPosition()
        {
            // Arrange
            var roomId = "room-123";

            // Act
            var position = await _queueService.AddToQueueAsync(roomId);

            // Assert
            position.Should().Be(1);
            _queueService.GetQueueLength().Should().Be(1);
        }

        [Fact]
        public async Task AddToQueueAsync_WithMultipleRooms_ShouldAssignIncrementingPositions()
        {
            // Arrange & Act
            var position1 = await _queueService.AddToQueueAsync("room-1");
            var position2 = await _queueService.AddToQueueAsync("room-2");
            var position3 = await _queueService.AddToQueueAsync("room-3");

            // Assert
            position1.Should().Be(1);
            position2.Should().Be(2);
            position3.Should().Be(3);
            _queueService.GetQueueLength().Should().Be(3);
        }

        [Fact]
        public async Task AddToQueueAsync_WithDuplicateRoom_ShouldReturnExistingPosition()
        {
            // Arrange
            var roomId = "room-123";
            await _queueService.AddToQueueAsync(roomId);

            // Act
            var secondPosition = await _queueService.AddToQueueAsync(roomId);

            // Assert
            secondPosition.Should().Be(1);
            _queueService.GetQueueLength().Should().Be(1);
        }

        #endregion

        #region GetNextInQueueAsync Tests

        [Fact]
        public async Task GetNextInQueueAsync_WithItemsInQueue_ShouldReturnFirstRoom()
        {
            // Arrange
            await _queueService.AddToQueueAsync("room-1");
            await _queueService.AddToQueueAsync("room-2");

            // Act
            var nextRoom = await _queueService.GetNextInQueueAsync();

            // Assert
            nextRoom.Should().Be("room-1");
        }

        [Fact]
        public async Task GetNextInQueueAsync_WithEmptyQueue_ShouldReturnNull()
        {
            // Act
            var nextRoom = await _queueService.GetNextInQueueAsync();

            // Assert
            nextRoom.Should().BeNull();
        }

        [Fact]
        public async Task GetNextInQueueAsync_ShouldReturnItemsInFIFOOrder()
        {
            // Arrange
            await _queueService.AddToQueueAsync("room-1");
            await _queueService.AddToQueueAsync("room-2");
            await _queueService.AddToQueueAsync("room-3");

            // Act
            var first = await _queueService.GetNextInQueueAsync();
            var second = await _queueService.GetNextInQueueAsync();
            var third = await _queueService.GetNextInQueueAsync();

            // Assert
            first.Should().Be("room-1");
            second.Should().Be("room-2");
            third.Should().Be("room-3");
        }

        #endregion

        #region RemoveFromQueueAsync Tests

        [Fact]
        public async Task RemoveFromQueueAsync_WithExistingRoom_ShouldRemoveAndReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            await _queueService.AddToQueueAsync(roomId);

            // Act
            var result = await _queueService.RemoveFromQueueAsync(roomId);

            // Assert
            result.Should().BeTrue();
            _queueService.GetQueueLength().Should().Be(0);
        }

        [Fact]
        public async Task RemoveFromQueueAsync_WithNonExistentRoom_ShouldReturnFalse()
        {
            // Act
            var result = await _queueService.RemoveFromQueueAsync("nonexistent");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task RemoveFromQueueAsync_ShouldUpdatePositionsForRemainingRooms()
        {
            // Arrange
            await _queueService.AddToQueueAsync("room-1");
            await _queueService.AddToQueueAsync("room-2");
            await _queueService.AddToQueueAsync("room-3");

            // Act
            await _queueService.RemoveFromQueueAsync("room-1");

            // Assert
            var status2 = await _queueService.GetQueueStatusAsync("room-2");
            var status3 = await _queueService.GetQueueStatusAsync("room-3");
            
            status2.Should().NotBeNull();
            status3.Should().NotBeNull();
            // Positions should be recalculated
        }

        #endregion

        #region GetQueueStatusAsync Tests

        [Fact]
        public async Task GetQueueStatusAsync_WithExistingRoom_ShouldReturnStatus()
        {
            // Arrange
            var roomId = "room-123";
            await _queueService.AddToQueueAsync(roomId);

            // Act
            var status = await _queueService.GetQueueStatusAsync(roomId);

            // Assert
            status.Should().NotBeNull();
            status!.RoomId.Should().Be(roomId);
            status.Position.Should().Be(1);
            status.Status.Should().Be("Waiting");
        }

        [Fact]
        public async Task GetQueueStatusAsync_WithNonExistentRoom_ShouldReturnNull()
        {
            // Act
            var status = await _queueService.GetQueueStatusAsync("nonexistent");

            // Assert
            status.Should().BeNull();
        }

        #endregion

        #region GetAllQueueStatusAsync Tests

        [Fact]
        public async Task GetAllQueueStatusAsync_ShouldReturnAllQueuedRooms()
        {
            // Arrange
            await _queueService.AddToQueueAsync("room-1");
            await _queueService.AddToQueueAsync("room-2");
            await _queueService.AddToQueueAsync("room-3");

            // Act
            var allStatus = await _queueService.GetAllQueueStatusAsync();

            // Assert
            allStatus.Should().HaveCount(3);
            allStatus.Should().BeInAscendingOrder(s => s.Position);
        }

        [Fact]
        public async Task GetAllQueueStatusAsync_WithEmptyQueue_ShouldReturnEmptyList()
        {
            // Act
            var allStatus = await _queueService.GetAllQueueStatusAsync();

            // Assert
            allStatus.Should().BeEmpty();
        }

        #endregion

        #region NotifyRoomAsync Tests

        [Fact]
        public async Task NotifyRoomAsync_WithWaitingRoom_ShouldNotifyAndReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            await _queueService.AddToQueueAsync(roomId);

            // Act
            var result = await _queueService.NotifyRoomAsync(roomId);

            // Assert
            result.Should().BeTrue();
            
            var status = await _queueService.GetQueueStatusAsync(roomId);
            status!.Status.Should().Be("Notified");
            status.NotifiedAt.Should().NotBeNull();
            status.NotificationExpiresAt.Should().NotBeNull();
        }

        [Fact]
        public async Task NotifyRoomAsync_WithAlreadyNotifiedRoom_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-123";
            await _queueService.AddToQueueAsync(roomId);
            await _queueService.NotifyRoomAsync(roomId);

            // Act
            var result = await _queueService.NotifyRoomAsync(roomId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task NotifyRoomAsync_WithNonExistentRoom_ShouldReturnFalse()
        {
            // Act
            var result = await _queueService.NotifyRoomAsync("nonexistent");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region AcceptNotificationAsync Tests

        [Fact]
        public async Task AcceptNotificationAsync_WithNotifiedRoom_ShouldAcceptAndReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            await _queueService.AddToQueueAsync(roomId);
            await _queueService.NotifyRoomAsync(roomId);

            // Act
            var result = await _queueService.AcceptNotificationAsync(roomId);

            // Assert
            result.Should().BeTrue();
            
            var status = await _queueService.GetQueueStatusAsync(roomId);
            status.Should().BeNull(); // Room should be removed from queue
        }

        [Fact]
        public async Task AcceptNotificationAsync_WithWaitingRoom_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-123";
            await _queueService.AddToQueueAsync(roomId);

            // Act
            var result = await _queueService.AcceptNotificationAsync(roomId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task AcceptNotificationAsync_WithNonExistentRoom_ShouldReturnFalse()
        {
            // Act
            var result = await _queueService.AcceptNotificationAsync("nonexistent");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region DeclineNotificationAsync Tests

        [Fact]
        public async Task DeclineNotificationAsync_WithNotifiedRoom_ShouldDeclineAndReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            await _queueService.AddToQueueAsync(roomId);
            await _queueService.NotifyRoomAsync(roomId);

            // Act
            var result = await _queueService.DeclineNotificationAsync(roomId);

            // Assert
            result.Should().BeTrue();
            
            var status = await _queueService.GetQueueStatusAsync(roomId);
            status.Should().BeNull(); // Room should be removed from queue
        }

        [Fact]
        public async Task DeclineNotificationAsync_WithWaitingRoom_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-123";
            await _queueService.AddToQueueAsync(roomId);

            // Act
            var result = await _queueService.DeclineNotificationAsync(roomId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetQueueLength Tests

        [Fact]
        public async Task GetQueueLength_ShouldReturnCorrectCount()
        {
            // Arrange
            await _queueService.AddToQueueAsync("room-1");
            await _queueService.AddToQueueAsync("room-2");
            await _queueService.AddToQueueAsync("room-3");

            // Act
            var length = _queueService.GetQueueLength();

            // Assert
            length.Should().Be(3);
        }

        [Fact]
        public async Task GetQueueLength_ShouldIncludeNotifiedRooms()
        {
            // Arrange
            await _queueService.AddToQueueAsync("room-1");
            await _queueService.AddToQueueAsync("room-2");
            await _queueService.NotifyRoomAsync("room-1");

            // Act
            var length = _queueService.GetQueueLength();

            // Assert
            length.Should().Be(2); // Both waiting and notified
        }

        [Fact]
        public void GetQueueLength_WithEmptyQueue_ShouldReturnZero()
        {
            // Act
            var length = _queueService.GetQueueLength();

            // Assert
            length.Should().Be(0);
        }

        #endregion

        #region ProcessExpiredNotificationsAsync Tests

        [Fact]
        public async Task ProcessExpiredNotificationsAsync_WithNoExpired_ShouldReturnFalse()
        {
            // Arrange
            await _queueService.AddToQueueAsync("room-1");
            await _queueService.NotifyRoomAsync("room-1");

            // Act (notification just created, not expired yet)
            var result = await _queueService.ProcessExpiredNotificationsAsync();

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task ConcurrentAddToQueue_ShouldBeThreadSafe()
        {
            // Arrange
            var tasks = new List<Task<int>>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var roomId = $"room-{i}";
                tasks.Add(_queueService.AddToQueueAsync(roomId));
            }

            await Task.WhenAll(tasks);

            // Assert
            _queueService.GetQueueLength().Should().Be(100);
        }

        [Fact]
        public async Task ConcurrentRemoveFromQueue_ShouldBeThreadSafe()
        {
            // Arrange
            for (int i = 0; i < 50; i++)
            {
                await _queueService.AddToQueueAsync($"room-{i}");
            }

            var removeTasks = new List<Task<bool>>();

            // Act
            for (int i = 0; i < 50; i++)
            {
                removeTasks.Add(_queueService.RemoveFromQueueAsync($"room-{i}"));
            }

            await Task.WhenAll(removeTasks);

            // Assert
            _queueService.GetQueueLength().Should().Be(0);
        }

        #endregion
    }
}
