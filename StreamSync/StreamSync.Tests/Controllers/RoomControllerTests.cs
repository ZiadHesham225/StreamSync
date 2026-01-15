using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StreamSync.Services.Interfaces;
using StreamSync.Controllers;
using StreamSync.DTOs;
using StreamSync.Models;
using StreamSync.Models.RealTime;
using System.Security.Claims;

namespace StreamSync.Tests.Controllers
{
    public class RoomControllerTests
    {
        private readonly Mock<IRoomService> _mockRoomService;
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<ILogger<RoomController>> _mockLogger;
        private readonly Mock<IRoomStateService> _mockRoomStateService;
        private readonly RoomController _controller;

        public RoomControllerTests()
        {
            _mockRoomService = new Mock<IRoomService>();
            _mockUserManager = CreateMockUserManager();
            _mockLogger = new Mock<ILogger<RoomController>>();
            _mockRoomStateService = new Mock<IRoomStateService>();

            // Default setup - return 0 for count, empty lists
            _mockRoomStateService
                .Setup(r => r.GetParticipantCountAsync(It.IsAny<string>()))
                .ReturnsAsync(0);
            _mockRoomStateService
                .Setup(r => r.GetRoomParticipantsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<RoomParticipant>());
            _mockRoomStateService
                .Setup(r => r.GetRoomMessagesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ChatMessage>());

            _controller = new RoomController(
                _mockRoomService.Object,
                _mockRoomStateService.Object,
                _mockUserManager.Object,
                _mockLogger.Object);

            // Set up a default user context
            SetupUserContext("user-123", "testuser");
        }

        private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        private void SetupUserContext(string userId, string username)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, username)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region GetActiveRooms Tests

        [Fact]
        public async Task GetActiveRooms_WithoutPagination_ShouldReturnAllActiveRooms()
        {
            // Arrange
            var rooms = new List<RoomDto>
            {
                new RoomDto
                {
                    Id = "room-1",
                    Name = "Room 1",
                    VideoUrl = "https://youtube.com/1",
                    AdminId = "admin-1",
                    AdminName = "Admin One",
                    InviteCode = "ABC123",
                    IsActive = true
                },
                new RoomDto
                {
                    Id = "room-2",
                    Name = "Room 2",
                    VideoUrl = "https://youtube.com/2",
                    AdminId = "admin-2",
                    AdminName = "Admin Two",
                    InviteCode = "DEF456",
                    IsActive = true
                }
            };

            _mockRoomService.Setup(s => s.GetActiveRoomsAsync())
                .ReturnsAsync(rooms);

            // Act
            var result = await _controller.GetActiveRooms(null);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedRooms = okResult.Value.Should().BeAssignableTo<IEnumerable<RoomDto>>().Subject;
            returnedRooms.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetActiveRooms_WithPagination_ShouldReturnPagedResults()
        {
            // Arrange
            var pagination = new PaginationQueryDto { Page = 1, PageSize = 10 };
            var pagedResult = new PagedResultDto<RoomDto>
            {
                Data = new List<RoomDto>
                {
                    new RoomDto
                    {
                        Id = "room-1",
                        Name = "Room 1",
                        VideoUrl = "https://youtube.com/1",
                        AdminId = "admin-1",
                        AdminName = "Admin",
                        InviteCode = "ABC123",
                        IsActive = true
                    }
                },
                CurrentPage = 1,
                PageSize = 10,
                TotalCount = 1,
                TotalPages = 1
            };

            _mockRoomService.Setup(s => s.GetActiveRoomsAsync(pagination))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _controller.GetActiveRooms(pagination);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedResult = okResult.Value.Should().BeOfType<PagedResultDto<RoomDto>>().Subject;
            returnedResult.Data.Should().HaveCount(1);
            returnedResult.CurrentPage.Should().Be(1);
        }

        [Fact]
        public async Task GetActiveRooms_ShouldIncludeUserCounts()
        {
            // Arrange
            var roomId = "room-1";
            var rooms = new List<RoomDto>
            {
                new RoomDto
                {
                    Id = roomId,
                    Name = "Room 1",
                    VideoUrl = "https://youtube.com/1",
                    AdminId = "admin-1",
                    AdminName = "Admin",
                    InviteCode = "ABC123",
                    IsActive = true,
                    UserCount = 0
                }
            };

            // Setup mock to return participant count
            _mockRoomStateService
                .Setup(r => r.GetParticipantCountAsync(roomId))
                .ReturnsAsync(2);

            _mockRoomService.Setup(s => s.GetActiveRoomsAsync())
                .ReturnsAsync(rooms);

            // Act
            var result = await _controller.GetActiveRooms(null);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedRooms = okResult.Value.Should().BeAssignableTo<IEnumerable<RoomDto>>().Subject.ToList();
            returnedRooms.First().UserCount.Should().Be(2);
        }

        #endregion

        #region GetRoomParticipants Tests

        [Fact]
        public async Task GetRoomParticipants_WithValidRoom_ShouldReturnParticipants()
        {
            // Arrange
            var roomId = "room-123";
            var adminId = "admin-123";
            var room = new Room
            {
                Id = roomId,
                Name = "Test Room",
                AdminId = adminId
            };

            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId))
                .ReturnsAsync(room);

            var participants = new List<RoomParticipant>
            {
                new RoomParticipant(adminId, "conn-1", "Admin User", null, true),
                new RoomParticipant("user-2", "conn-2", "Regular User", null, false)
            };
            
            _mockRoomStateService
                .Setup(r => r.GetRoomParticipantsAsync(roomId))
                .ReturnsAsync(participants);

            // Act
            var result = await _controller.GetRoomParticipants(roomId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedParticipants = okResult.Value.Should().BeAssignableTo<List<RoomParticipantDto>>().Subject;
            returnedParticipants.Should().HaveCount(2);
            returnedParticipants.Should().Contain(p => p.Id == adminId && p.IsAdmin);
            returnedParticipants.Should().Contain(p => p.Id == "user-2" && !p.IsAdmin);
        }

        [Fact]
        public async Task GetRoomParticipants_WithNonExistentRoom_ShouldReturnNotFound()
        {
            // Arrange
            var roomId = "nonexistent";
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId))
                .Returns(Task.FromResult<Room?>(null));

            // Act
            var result = await _controller.GetRoomParticipants(roomId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region GetRoomMessages Tests

        [Fact]
        public async Task GetRoomMessages_WithValidRoom_ShouldReturnMessages()
        {
            // Arrange
            var roomId = "room-123";
            var room = new Room
            {
                Id = roomId,
                Name = "Test Room",
                AdminId = "admin-123"
            };

            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId))
                .ReturnsAsync(room);

            var messages = new List<ChatMessage>
            {
                new ChatMessage("user-1", "User One", null, "Hello!"),
                new ChatMessage("user-2", "User Two", null, "Hi there!")
            };
            
            _mockRoomStateService
                .Setup(r => r.GetRoomMessagesAsync(roomId))
                .ReturnsAsync(messages);

            // Act
            var result = await _controller.GetRoomMessages(roomId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedMessages = okResult.Value.Should().BeAssignableTo<IEnumerable<ChatMessageDto>>().Subject.ToList();
            returnedMessages.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetRoomMessages_WithNonExistentRoom_ShouldReturnNotFound()
        {
            // Arrange
            var roomId = "nonexistent";
            _mockRoomService.Setup(s => s.GetRoomByIdAsync(roomId))
                .Returns(Task.FromResult<Room?>(null));

            // Act
            var result = await _controller.GetRoomMessages(roomId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion
    }
}
