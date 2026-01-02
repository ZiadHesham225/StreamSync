using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StreamSync.Services.Interfaces;
using StreamSync.Controllers;
using StreamSync.DTOs;
using System.Security.Claims;

namespace StreamSync.Tests.Controllers
{
    public class UserControllerTests
    {
        private readonly Mock<IUserService> _mockUserService;
        private readonly Mock<ILogger<UserController>> _mockLogger;
        private readonly UserController _controller;

        public UserControllerTests()
        {
            _mockUserService = new Mock<IUserService>();
            _mockLogger = new Mock<ILogger<UserController>>();

            _controller = new UserController(
                _mockUserService.Object,
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

        #region GetProfile Tests

        [Fact]
        public async Task GetProfile_WithAuthenticatedUser_ShouldReturnProfile()
        {
            // Arrange
            var expectedProfile = new UserProfileDto
            {
                Id = "user-123",
                Email = "test@example.com",
                DisplayName = "Test User",
                AvatarUrl = "https://example.com/avatar.jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

            _mockUserService.Setup(s => s.GetUserProfileAsync("user-123"))
                .ReturnsAsync(expectedProfile);

            // Act
            var result = await _controller.GetProfile();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var profile = okResult.Value.Should().BeOfType<UserProfileDto>().Subject;
            profile.Id.Should().Be("user-123");
            profile.Email.Should().Be("test@example.com");
            profile.DisplayName.Should().Be("Test User");
        }

        [Fact]
        public async Task GetProfile_WithUnauthenticatedUser_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupAnonymousContext();

            // Act
            var result = await _controller.GetProfile();

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task GetProfile_WhenUserNotFound_ShouldReturnBadRequest()
        {
            // Arrange
            _mockUserService.Setup(s => s.GetUserProfileAsync("user-123"))
                .ThrowsAsync(new InvalidOperationException("User not found"));

            // Act
            var result = await _controller.GetProfile();

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetProfile_WhenExceptionOccurs_ShouldReturn500()
        {
            // Arrange
            _mockUserService.Setup(s => s.GetUserProfileAsync("user-123"))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetProfile();

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region UpdateProfile Tests

        [Fact]
        public async Task UpdateProfile_WithValidData_ShouldReturnUpdatedProfile()
        {
            // Arrange
            var updateDto = new UpdateUserProfileDto
            {
                DisplayName = "New Name",
                AvatarUrl = "https://example.com/new-avatar.jpg"
            };

            var expectedProfile = new UserProfileDto
            {
                Id = "user-123",
                Email = "test@example.com",
                DisplayName = "New Name",
                AvatarUrl = "https://example.com/new-avatar.jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

            _mockUserService.Setup(s => s.UpdateUserProfileAsync("user-123", updateDto))
                .ReturnsAsync(expectedProfile);

            // Act
            var result = await _controller.UpdateProfile(updateDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var profile = okResult.Value.Should().BeOfType<UserProfileDto>().Subject;
            profile.DisplayName.Should().Be("New Name");
            profile.AvatarUrl.Should().Be("https://example.com/new-avatar.jpg");
        }

        [Fact]
        public async Task UpdateProfile_WithUnauthenticatedUser_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupAnonymousContext();
            var updateDto = new UpdateUserProfileDto { DisplayName = "New Name" };

            // Act
            var result = await _controller.UpdateProfile(updateDto);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task UpdateProfile_WhenUpdateFails_ShouldReturnBadRequest()
        {
            // Arrange
            var updateDto = new UpdateUserProfileDto { DisplayName = "New Name" };

            _mockUserService.Setup(s => s.UpdateUserProfileAsync("user-123", updateDto))
                .ThrowsAsync(new InvalidOperationException("Failed to update profile"));

            // Act
            var result = await _controller.UpdateProfile(updateDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task UpdateProfile_WhenExceptionOccurs_ShouldReturn500()
        {
            // Arrange
            var updateDto = new UpdateUserProfileDto { DisplayName = "New Name" };

            _mockUserService.Setup(s => s.UpdateUserProfileAsync("user-123", updateDto))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.UpdateProfile(updateDto);

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region ChangePassword Tests

        [Fact]
        public async Task ChangePassword_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var changePasswordDto = new ChangePasswordDto
            {
                CurrentPassword = "OldPassword123!",
                NewPassword = "NewPassword123!"
            };

            _mockUserService.Setup(s => s.ChangePasswordAsync("user-123", changePasswordDto))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.ChangePassword(changePasswordDto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ChangePassword_WithUnauthenticatedUser_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupAnonymousContext();
            var changePasswordDto = new ChangePasswordDto
            {
                CurrentPassword = "OldPassword123!",
                NewPassword = "NewPassword123!"
            };

            // Act
            var result = await _controller.ChangePassword(changePasswordDto);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task ChangePassword_WithIncorrectCurrentPassword_ShouldReturnBadRequest()
        {
            // Arrange
            var changePasswordDto = new ChangePasswordDto
            {
                CurrentPassword = "WrongPassword",
                NewPassword = "NewPassword123!"
            };

            _mockUserService.Setup(s => s.ChangePasswordAsync("user-123", changePasswordDto))
                .ThrowsAsync(new InvalidOperationException("Failed to change password: Incorrect password"));

            // Act
            var result = await _controller.ChangePassword(changePasswordDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task ChangePassword_WhenExceptionOccurs_ShouldReturn500()
        {
            // Arrange
            var changePasswordDto = new ChangePasswordDto
            {
                CurrentPassword = "OldPassword123!",
                NewPassword = "NewPassword123!"
            };

            _mockUserService.Setup(s => s.ChangePasswordAsync("user-123", changePasswordDto))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.ChangePassword(changePasswordDto);

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion
    }
}
