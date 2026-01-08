using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using StreamSync.Services.Interfaces;
using StreamSync.Services;
using StreamSync.Data;
using StreamSync.DataAccess.Interfaces;
using StreamSync.DTOs;
using StreamSync.Models;

namespace StreamSync.Tests.Services
{
    public class RoomServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<IVirtualBrowserQueueService> _mockQueueService;
        private readonly Mock<ILogger<RoomService>> _mockLogger;
        private readonly Mock<IRoomStateService> _mockRoomStateService;
        private readonly RoomService _roomService;

        public RoomServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserManager = CreateMockUserManager();
            _mockQueueService = new Mock<IVirtualBrowserQueueService>();
            _mockLogger = new Mock<ILogger<RoomService>>();
            _mockRoomStateService = new Mock<IRoomStateService>();

            _roomService = new RoomService(
                _mockUnitOfWork.Object,
                _mockUserManager.Object,
                _mockQueueService.Object,
                _mockRoomStateService.Object,
                _mockLogger.Object);
        }

        private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        #region CreateRoomAsync Tests

        [Fact]
        public async Task CreateRoomAsync_WithValidData_ShouldCreateRoom()
        {
            // Arrange
            var userId = "user-123";
            var roomCreateDto = new RoomCreateDto
            {
                Name = "Test Room",
                VideoUrl = "https://youtube.com/watch?v=test",
                IsPrivate = false,
                AutoPlay = true,
                SyncMode = "strict"
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _roomService.CreateRoomAsync(roomCreateDto, userId);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Test Room");
            result.VideoUrl.Should().Be("https://youtube.com/watch?v=test");
            result.AdminId.Should().Be(userId);
            result.IsActive.Should().BeTrue();
            result.IsPrivate.Should().BeFalse();
            result.AutoPlay.Should().BeTrue();
            result.SyncMode.Should().Be("strict");
            result.InviteCode.Should().NotBeNullOrEmpty();
            result.InviteCode.Should().HaveLength(8);
            
            mockRoomRepo.Verify(r => r.CreateAsync(It.IsAny<Room>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateRoomAsync_WithPrivateRoomAndPassword_ShouldHashPassword()
        {
            // Arrange
            var userId = "user-123";
            var roomCreateDto = new RoomCreateDto
            {
                Name = "Private Room",
                VideoUrl = "https://youtube.com/watch?v=test",
                IsPrivate = true,
                Password = "secretpassword123",
                AutoPlay = true,
                SyncMode = "strict"
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _roomService.CreateRoomAsync(roomCreateDto, userId);

            // Assert
            result.Should().NotBeNull();
            result!.IsPrivate.Should().BeTrue();
            result.PasswordHash.Should().NotBeNullOrEmpty();
            result.PasswordHash.Should().NotBe("secretpassword123"); // Should be hashed
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateRoomAsync_WithEmptyName_ShouldReturnNull(string? invalidName)
        {
            // Arrange
            var userId = "user-123";
            var roomCreateDto = new RoomCreateDto
            {
                Name = invalidName!,
                VideoUrl = "https://youtube.com/watch?v=test",
                SyncMode = "strict"
            };

            // Act
            var result = await _roomService.CreateRoomAsync(roomCreateDto, userId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task CreateRoomAsync_WithTrimmedName_ShouldTrimWhitespace()
        {
            // Arrange
            var userId = "user-123";
            var roomCreateDto = new RoomCreateDto
            {
                Name = "  Test Room  ",
                VideoUrl = "https://youtube.com/watch?v=test",
                SyncMode = "strict"
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _roomService.CreateRoomAsync(roomCreateDto, userId);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Test Room");
        }

        #endregion

        #region GetRoomByIdAsync Tests

        [Fact]
        public async Task GetRoomByIdAsync_WithExistingRoom_ShouldReturnRoom()
        {
            // Arrange
            var roomId = "room-123";
            var expectedRoom = new Room
            {
                Id = roomId,
                Name = "Test Room",
                AdminId = "user-123",
                IsActive = true
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(expectedRoom);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.GetRoomByIdAsync(roomId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedRoom);
        }

        [Fact]
        public async Task GetRoomByIdAsync_WithNonExistingRoom_ShouldReturnNull()
        {
            // Arrange
            var roomId = "nonexistent-room";
            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).Returns(Task.FromResult<Room?>(null));
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.GetRoomByIdAsync(roomId);

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetRoomByIdAsync_WithInvalidRoomId_ShouldReturnNull(string? invalidId)
        {
            // Act
            var result = await _roomService.GetRoomByIdAsync(invalidId!);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetRoomByInviteCodeAsync Tests

        [Fact]
        public async Task GetRoomByInviteCodeAsync_WithValidCode_ShouldReturnRoom()
        {
            // Arrange
            var inviteCode = "ABC12345";
            var expectedRoom = new Room
            {
                Id = "room-123",
                Name = "Test Room",
                InviteCode = inviteCode,
                IsActive = true
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetRoomByInviteCodeAsync(inviteCode)).ReturnsAsync(expectedRoom);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.GetRoomByInviteCodeAsync(inviteCode);

            // Assert
            result.Should().NotBeNull();
            result!.InviteCode.Should().Be(inviteCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetRoomByInviteCodeAsync_WithInvalidCode_ShouldReturnNull(string? invalidCode)
        {
            // Act
            var result = await _roomService.GetRoomByInviteCodeAsync(invalidCode!);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetActiveRoomsAsync Tests

        [Fact]
        public async Task GetActiveRoomsAsync_ShouldReturnActiveRooms()
        {
            // Arrange
            var activeRooms = new List<Room>
            {
                new Room
                {
                    Id = "room-1",
                    Name = "Room 1",
                    VideoUrl = "https://youtube.com/1",
                    AdminId = "user-1",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    InviteCode = "CODE1234",
                    Admin = new ApplicationUser { DisplayName = "Admin 1", UserName = "admin1" }
                },
                new Room
                {
                    Id = "room-2",
                    Name = "Room 2",
                    VideoUrl = "https://youtube.com/2",
                    AdminId = "user-2",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    InviteCode = "CODE5678",
                    Admin = new ApplicationUser { DisplayName = "Admin 2", UserName = "admin2" }
                }
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetActiveRoomsAsync()).ReturnsAsync(activeRooms);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.GetActiveRoomsAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().AllSatisfy(r => r.IsActive.Should().BeTrue());
        }

        [Fact]
        public async Task GetActiveRoomsAsync_WithNoRooms_ShouldReturnEmptyCollection()
        {
            // Arrange
            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetActiveRoomsAsync()).ReturnsAsync(new List<Room>());
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.GetActiveRoomsAsync();

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region UpdateRoomAsync Tests

        [Fact]
        public async Task UpdateRoomAsync_WithValidData_ShouldUpdateRoom()
        {
            // Arrange
            var userId = "user-123";
            var roomId = "room-123";
            var existingRoom = new Room
            {
                Id = roomId,
                Name = "Old Name",
                VideoUrl = "https://youtube.com/old",
                AdminId = userId,
                IsActive = true
            };

            var updateDto = new RoomUpdateDto
            {
                RoomId = roomId,
                Name = "New Name",
                VideoUrl = "https://youtube.com/new",
                IsPrivate = true,
                AutoPlay = false,
                SyncMode = "flexible"
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(existingRoom);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _roomService.UpdateRoomAsync(updateDto, userId);

            // Assert
            result.Should().BeTrue();
            existingRoom.Name.Should().Be("New Name");
            existingRoom.VideoUrl.Should().Be("https://youtube.com/new");
            mockRoomRepo.Verify(r => r.Update(existingRoom), Times.Once);
        }

        [Fact]
        public async Task UpdateRoomAsync_WithNonAdmin_ShouldReturnFalse()
        {
            // Arrange
            var adminId = "admin-123";
            var otherUserId = "other-user";
            var roomId = "room-123";
            var existingRoom = new Room
            {
                Id = roomId,
                Name = "Test Room",
                AdminId = adminId,
                IsActive = true
            };

            var updateDto = new RoomUpdateDto { RoomId = roomId, Name = "New Name" };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(existingRoom);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.UpdateRoomAsync(updateDto, otherUserId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateRoomAsync_WithInactiveRoom_ShouldReturnFalse()
        {
            // Arrange
            var userId = "user-123";
            var roomId = "room-123";
            var inactiveRoom = new Room
            {
                Id = roomId,
                AdminId = userId,
                IsActive = false
            };

            var updateDto = new RoomUpdateDto { RoomId = roomId, Name = "New Name" };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(inactiveRoom);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.UpdateRoomAsync(updateDto, userId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region EndRoomAsync Tests

        [Fact]
        public async Task EndRoomAsync_WithValidAdmin_ShouldEndRoom()
        {
            // Arrange
            var userId = "user-123";
            var roomId = "room-123";
            var activeRoom = new Room
            {
                Id = roomId,
                AdminId = userId,
                IsActive = true
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            var mockVirtualBrowserRepo = new Mock<IVirtualBrowserRepository>();
            
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(activeRoom);
            mockVirtualBrowserRepo.Setup(v => v.GetByRoomIdAsync(roomId)).ReturnsAsync((VirtualBrowser?)null);
            _mockQueueService.Setup(q => q.RemoveFromQueueAsync(roomId)).ReturnsAsync(true);
            
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);
            _mockUnitOfWork.Setup(u => u.VirtualBrowsers).Returns(mockVirtualBrowserRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _roomService.EndRoomAsync(roomId, userId);

            // Assert
            result.Should().BeTrue();
            activeRoom.IsActive.Should().BeFalse();
            activeRoom.EndedAt.Should().NotBeNull();
            mockRoomRepo.Verify(r => r.Update(activeRoom), Times.Once);
        }

        [Fact]
        public async Task EndRoomAsync_WithNonAdmin_ShouldReturnFalse()
        {
            // Arrange
            var adminId = "admin-123";
            var otherUserId = "other-user";
            var roomId = "room-123";
            var activeRoom = new Room
            {
                Id = roomId,
                AdminId = adminId,
                IsActive = true
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(activeRoom);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.EndRoomAsync(roomId, otherUserId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region ValidateRoomPasswordAsync Tests

        [Fact]
        public async Task ValidateRoomPasswordAsync_WithCorrectPassword_ShouldReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            var password = "correctpassword";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var privateRoom = new Room
            {
                Id = roomId,
                IsPrivate = true,
                PasswordHash = hashedPassword
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(privateRoom);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.ValidateRoomPasswordAsync(roomId, password);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateRoomPasswordAsync_WithIncorrectPassword_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-123";
            var correctPassword = "correctpassword";
            var incorrectPassword = "wrongpassword";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(correctPassword);
            var privateRoom = new Room
            {
                Id = roomId,
                IsPrivate = true,
                PasswordHash = hashedPassword
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(privateRoom);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.ValidateRoomPasswordAsync(roomId, incorrectPassword);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateRoomPasswordAsync_WithPublicRoom_ShouldReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            var publicRoom = new Room
            {
                Id = roomId,
                IsPrivate = false
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(publicRoom);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.ValidateRoomPasswordAsync(roomId, null);

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region IsUserAdminAsync Tests

        [Fact]
        public async Task IsUserAdminAsync_WithAdmin_ShouldReturnTrue()
        {
            // Arrange
            var roomId = "room-123";
            var adminId = "user-123";
            var room = new Room
            {
                Id = roomId,
                AdminId = adminId
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.IsUserAdminAsync(roomId, adminId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsUserAdminAsync_WithNonAdmin_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-123";
            var adminId = "admin-123";
            var nonAdminId = "other-user";
            var room = new Room
            {
                Id = roomId,
                AdminId = adminId
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.IsUserAdminAsync(roomId, nonAdminId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region UpdatePlaybackStateAsync Tests

        [Fact]
        public async Task UpdatePlaybackStateAsync_WithValidRoom_ShouldUpdateState()
        {
            // Arrange
            var roomId = "room-123";
            var userId = "user-123";
            var room = new Room
            {
                Id = roomId,
                IsActive = true,
                CurrentPosition = 0,
                IsPlaying = false
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _roomService.UpdatePlaybackStateAsync(roomId, userId, 120.5, true);

            // Assert
            result.Should().BeTrue();
            room.CurrentPosition.Should().Be(120.5);
            room.IsPlaying.Should().BeTrue();
        }

        [Fact]
        public async Task UpdatePlaybackStateAsync_WithNegativePosition_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "room-123";
            var userId = "user-123";

            // Act
            var result = await _roomService.UpdatePlaybackStateAsync(roomId, userId, -10, true);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GenerateInviteLink Tests

        [Fact]
        public async Task GenerateInviteLink_WithActiveRoom_ShouldReturnLink()
        {
            // Arrange
            var roomId = "room-123";
            var inviteCode = "ABC12345";
            var room = new Room
            {
                Id = roomId,
                InviteCode = inviteCode,
                IsActive = true
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.GenerateInviteLink(roomId);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain(inviteCode);
            result.Should().Be($"/watch-party/join/{inviteCode}");
        }

        [Fact]
        public async Task GenerateInviteLink_WithInactiveRoom_ShouldReturnNull()
        {
            // Arrange
            var roomId = "room-123";
            var room = new Room
            {
                Id = roomId,
                IsActive = false
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.GenerateInviteLink(roomId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region UpdateSyncModeAsync Tests

        [Fact]
        public async Task UpdateSyncModeAsync_WithValidRoom_ShouldUpdateMode()
        {
            // Arrange
            var roomId = "room-123";
            var room = new Room
            {
                Id = roomId,
                SyncMode = "strict"
            };

            var mockRoomRepo = new Mock<IRoomRepository>();
            var mockGenericRoomRepo = new Mock<IGenericRepository<Room>>();
            
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);
            _mockUnitOfWork.Setup(u => u.GenericRooms).Returns(mockGenericRoomRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _roomService.UpdateSyncModeAsync(roomId, "flexible");

            // Assert
            result.Should().BeTrue();
            room.SyncMode.Should().Be("flexible");
        }

        [Fact]
        public async Task UpdateSyncModeAsync_WithNonExistentRoom_ShouldReturnFalse()
        {
            // Arrange
            var roomId = "nonexistent-room";
            var mockRoomRepo = new Mock<IRoomRepository>();
            mockRoomRepo.Setup(r => r.GetByIdAsync(roomId)).Returns(Task.FromResult<Room?>(null));
            _mockUnitOfWork.Setup(u => u.Rooms).Returns(mockRoomRepo.Object);

            // Act
            var result = await _roomService.UpdateSyncModeAsync(roomId, "flexible");

            // Assert
            result.Should().BeFalse();
        }

        #endregion
    }
}
