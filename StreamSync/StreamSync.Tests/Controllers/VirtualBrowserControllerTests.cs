using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.Controllers;
using StreamSync.DTOs;
using StreamSync.Models;
using System.Security.Claims;

namespace StreamSync.Tests.Controllers
{
    public class VirtualBrowserControllerTests
    {
        private readonly Mock<IVirtualBrowserService> _mockVirtualBrowserService;
        private readonly Mock<IRoomService> _mockRoomService;
        private readonly Mock<ILogger<VirtualBrowserController>> _mockLogger;
        private readonly VirtualBrowserController _controller;

        public VirtualBrowserControllerTests()
        {
            _mockVirtualBrowserService = new Mock<IVirtualBrowserService>();
            _mockRoomService = new Mock<IRoomService>();
            _mockLogger = new Mock<ILogger<VirtualBrowserController>>();

            _controller = new VirtualBrowserController(
                _mockVirtualBrowserService.Object,
                _mockRoomService.Object,
                _mockLogger.Object);

            SetupUserContext("user-123", "testuser");
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

        private void SetupAnonymousContext()
        {
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        #region RequestVirtualBrowser Tests

        [Fact]
        public async Task RequestVirtualBrowser_WithAuthorizedUser_ShouldReturnBrowser()
        {
            // Arrange
            var request = new VirtualBrowserRequestDto { RoomId = "room-123" };
            var expectedBrowser = new VirtualBrowserDto
            {
                Id = "browser-123",
                RoomId = "room-123",
                BrowserUrl = "http://localhost:8080",
                Status = "Allocated"
            };

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(request.RoomId, "user-123"))
                .ReturnsAsync(true);
            _mockVirtualBrowserService.Setup(s => s.RequestVirtualBrowserAsync(request.RoomId))
                .ReturnsAsync(expectedBrowser);

            // Act
            var result = await _controller.RequestVirtualBrowser(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var browser = okResult.Value.Should().BeOfType<VirtualBrowserDto>().Subject;
            browser.Id.Should().Be("browser-123");
        }

        [Fact]
        public async Task RequestVirtualBrowser_WithNoBrowserAvailable_ShouldReturnQueueStatus()
        {
            // Arrange
            var request = new VirtualBrowserRequestDto { RoomId = "room-123" };
            var queueStatus = new VirtualBrowserQueueDto
            {
                RoomId = "room-123",
                Position = 3,
                Status = "Waiting"
            };

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(request.RoomId, "user-123"))
                .ReturnsAsync(true);
            _mockVirtualBrowserService.Setup(s => s.RequestVirtualBrowserAsync(request.RoomId))
                .ReturnsAsync((VirtualBrowserDto?)null);
            _mockVirtualBrowserService.Setup(s => s.GetRoomQueueStatusAsync(request.RoomId))
                .ReturnsAsync(queueStatus);

            // Act
            var result = await _controller.RequestVirtualBrowser(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task RequestVirtualBrowser_WithUnauthorizedUser_ShouldReturnForbid()
        {
            // Arrange
            var request = new VirtualBrowserRequestDto { RoomId = "room-123" };

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(request.RoomId, "user-123"))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.RequestVirtualBrowser(request);

            // Assert
            result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task RequestVirtualBrowser_WithUnauthenticated_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupAnonymousContext();
            var request = new VirtualBrowserRequestDto { RoomId = "room-123" };

            // Act
            var result = await _controller.RequestVirtualBrowser(request);

            // Assert
            result.Should().BeOfType<UnauthorizedResult>();
        }

        #endregion

        #region ReleaseVirtualBrowser Tests

        [Fact]
        public async Task ReleaseVirtualBrowser_WithAuthorizedUser_ShouldReleaseSuccessfully()
        {
            // Arrange
            var roomId = "room-123";

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(roomId, "user-123"))
                .ReturnsAsync(true);
            _mockVirtualBrowserService.Setup(s => s.ReleaseVirtualBrowserAsync(roomId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.ReleaseVirtualBrowser(roomId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ReleaseVirtualBrowser_WithFailedRelease_ShouldReturnBadRequest()
        {
            // Arrange
            var roomId = "room-123";

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(roomId, "user-123"))
                .ReturnsAsync(true);
            _mockVirtualBrowserService.Setup(s => s.ReleaseVirtualBrowserAsync(roomId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.ReleaseVirtualBrowser(roomId);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task ReleaseVirtualBrowser_WithUnauthorizedUser_ShouldReturnForbid()
        {
            // Arrange
            var roomId = "room-123";

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(roomId, "user-123"))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.ReleaseVirtualBrowser(roomId);

            // Assert
            result.Should().BeOfType<ForbidResult>();
        }

        #endregion

        #region GetRoomVirtualBrowser Tests

        [Fact]
        public async Task GetRoomVirtualBrowser_WithExistingBrowser_ShouldReturnBrowser()
        {
            // Arrange
            var roomId = "room-123";
            var browser = new VirtualBrowserDto
            {
                Id = "browser-123",
                RoomId = roomId,
                BrowserUrl = "http://localhost:8080",
                Status = "InUse"
            };

            _mockVirtualBrowserService.Setup(s => s.GetRoomVirtualBrowserAsync(roomId))
                .ReturnsAsync(browser);

            // Act
            var result = await _controller.GetRoomVirtualBrowser(roomId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedBrowser = okResult.Value.Should().BeOfType<VirtualBrowserDto>().Subject;
            returnedBrowser.Id.Should().Be("browser-123");
        }

        [Fact]
        public async Task GetRoomVirtualBrowser_WithQueueEntry_ShouldReturnQueueStatus()
        {
            // Arrange
            var roomId = "room-123";
            var queueStatus = new VirtualBrowserQueueDto
            {
                RoomId = roomId,
                Position = 2,
                Status = "Waiting"
            };

            _mockVirtualBrowserService.Setup(s => s.GetRoomVirtualBrowserAsync(roomId))
                .ReturnsAsync((VirtualBrowserDto?)null);
            _mockVirtualBrowserService.Setup(s => s.GetRoomQueueStatusAsync(roomId))
                .ReturnsAsync(queueStatus);

            // Act
            var result = await _controller.GetRoomVirtualBrowser(roomId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetRoomVirtualBrowser_WithNoBrowserOrQueue_ShouldReturnNotFound()
        {
            // Arrange
            var roomId = "room-123";

            _mockVirtualBrowserService.Setup(s => s.GetRoomVirtualBrowserAsync(roomId))
                .ReturnsAsync((VirtualBrowserDto?)null);
            _mockVirtualBrowserService.Setup(s => s.GetRoomQueueStatusAsync(roomId))
                .ReturnsAsync((VirtualBrowserQueueDto?)null);

            // Act
            var result = await _controller.GetRoomVirtualBrowser(roomId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region GetRoomCooldownStatus Tests

        [Fact]
        public async Task GetRoomCooldownStatus_ShouldReturnCooldownInfo()
        {
            // Arrange
            var roomId = "room-123";
            var cooldownInfo = new { isOnCooldown = false, remainingSeconds = 0 };

            _mockVirtualBrowserService.Setup(s => s.GetRoomCooldownStatusAsync(roomId))
                .ReturnsAsync(cooldownInfo);

            // Act
            var result = await _controller.GetRoomCooldownStatus(roomId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        #endregion

        #region AcceptQueueNotification Tests

        [Fact]
        public async Task AcceptQueueNotification_WithAuthorizedUser_ShouldAccept()
        {
            // Arrange
            var roomId = "room-123";

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(roomId, "user-123"))
                .ReturnsAsync(true);
            _mockVirtualBrowserService.Setup(s => s.AcceptQueueNotificationAsync(roomId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.AcceptQueueNotification(roomId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task AcceptQueueNotification_WithFailedAccept_ShouldReturnBadRequest()
        {
            // Arrange
            var roomId = "room-123";

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(roomId, "user-123"))
                .ReturnsAsync(true);
            _mockVirtualBrowserService.Setup(s => s.AcceptQueueNotificationAsync(roomId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.AcceptQueueNotification(roomId);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region DeclineQueueNotification Tests

        [Fact]
        public async Task DeclineQueueNotification_WithAuthorizedUser_ShouldDecline()
        {
            // Arrange
            var roomId = "room-123";

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(roomId, "user-123"))
                .ReturnsAsync(true);
            _mockVirtualBrowserService.Setup(s => s.DeclineQueueNotificationAsync(roomId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeclineQueueNotification(roomId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        #endregion

        #region CancelQueue Tests

        [Fact]
        public async Task CancelQueue_WithAuthorizedUser_ShouldCancel()
        {
            // Arrange
            var roomId = "room-123";

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(roomId, "user-123"))
                .ReturnsAsync(true);
            _mockVirtualBrowserService.Setup(s => s.CancelQueueAsync(roomId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.CancelQueue(roomId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task CancelQueue_WithUnauthorizedUser_ShouldReturnForbid()
        {
            // Arrange
            var roomId = "room-123";

            _mockRoomService.Setup(s => s.CanUserControlRoomAsync(roomId, "user-123"))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.CancelQueue(roomId);

            // Assert
            result.Should().BeOfType<ForbidResult>();
        }

        #endregion
    }
}
