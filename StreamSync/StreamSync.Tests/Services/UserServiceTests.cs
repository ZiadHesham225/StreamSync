using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using StreamSync.Services;
using StreamSync.DTOs;
using StreamSync.Models;

namespace StreamSync.Tests.Services
{
    public class UserServiceTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<ILogger<UserService>> _mockLogger;
        private readonly UserService _userService;

        public UserServiceTests()
        {
            _mockUserManager = CreateMockUserManager();
            _mockLogger = new Mock<ILogger<UserService>>();

            _userService = new UserService(
                _mockUserManager.Object,
                _mockLogger.Object);
        }

        private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        #region GetUserProfileAsync Tests

        [Fact]
        public async Task GetUserProfileAsync_WithExistingUser_ShouldReturnProfile()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                Id = userId,
                Email = "test@example.com",
                DisplayName = "Test User",
                AvatarUrl = "https://example.com/avatar.jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync(user);

            // Act
            var result = await _userService.GetUserProfileAsync(userId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(userId);
            result.Email.Should().Be("test@example.com");
            result.DisplayName.Should().Be("Test User");
            result.AvatarUrl.Should().Be("https://example.com/avatar.jpg");
        }

        [Fact]
        public async Task GetUserProfileAsync_WithNonExistentUser_ShouldThrowException()
        {
            // Arrange
            var userId = "nonexistent-user";
            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync((ApplicationUser?)null);

            // Act & Assert
            var act = () => _userService.GetUserProfileAsync(userId);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("User not found");
        }

        [Fact]
        public async Task GetUserProfileAsync_WithNullEmail_ShouldReturnEmptyEmail()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                Id = userId,
                Email = null,
                DisplayName = "Test User"
            };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync(user);

            // Act
            var result = await _userService.GetUserProfileAsync(userId);

            // Assert
            result.Email.Should().BeEmpty();
        }

        #endregion

        #region UpdateUserProfileAsync Tests

        [Fact]
        public async Task UpdateUserProfileAsync_WithValidData_ShouldUpdateAndReturnProfile()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                Id = userId,
                Email = "test@example.com",
                DisplayName = "Old Name",
                AvatarUrl = "https://example.com/old-avatar.jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

            var updateDto = new UpdateUserProfileDto
            {
                DisplayName = "New Name",
                AvatarUrl = "https://example.com/new-avatar.jpg"
            };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _userService.UpdateUserProfileAsync(userId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.DisplayName.Should().Be("New Name");
            result.AvatarUrl.Should().Be("https://example.com/new-avatar.jpg");
            
            user.DisplayName.Should().Be("New Name");
            user.AvatarUrl.Should().Be("https://example.com/new-avatar.jpg");
        }

        [Fact]
        public async Task UpdateUserProfileAsync_WithNonExistentUser_ShouldThrowException()
        {
            // Arrange
            var userId = "nonexistent-user";
            var updateDto = new UpdateUserProfileDto
            {
                DisplayName = "New Name"
            };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync((ApplicationUser?)null);

            // Act & Assert
            var act = () => _userService.UpdateUserProfileAsync(userId, updateDto);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("User not found");
        }

        [Fact]
        public async Task UpdateUserProfileAsync_WithFailedUpdate_ShouldThrowException()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                Id = userId,
                DisplayName = "Old Name"
            };

            var updateDto = new UpdateUserProfileDto
            {
                DisplayName = "New Name"
            };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

            // Act & Assert
            var act = () => _userService.UpdateUserProfileAsync(userId, updateDto);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Failed to update profile: Update failed");
        }

        #endregion

        #region ChangePasswordAsync Tests

        [Fact]
        public async Task ChangePasswordAsync_WithValidCurrentPassword_ShouldChangePassword()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser { Id = userId };
            var changePasswordDto = new ChangePasswordDto
            {
                CurrentPassword = "OldPassword123!",
                NewPassword = "NewPassword123!"
            };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            await _userService.ChangePasswordAsync(userId, changePasswordDto);

            // Assert
            _mockUserManager.Verify(m => m.ChangePasswordAsync(
                user, 
                changePasswordDto.CurrentPassword, 
                changePasswordDto.NewPassword), Times.Once);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithNonExistentUser_ShouldThrowException()
        {
            // Arrange
            var userId = "nonexistent-user";
            var changePasswordDto = new ChangePasswordDto
            {
                CurrentPassword = "OldPassword123!",
                NewPassword = "NewPassword123!"
            };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync((ApplicationUser?)null);

            // Act & Assert
            var act = () => _userService.ChangePasswordAsync(userId, changePasswordDto);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("User not found");
        }

        [Fact]
        public async Task ChangePasswordAsync_WithIncorrectCurrentPassword_ShouldThrowException()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser { Id = userId };
            var changePasswordDto = new ChangePasswordDto
            {
                CurrentPassword = "WrongPassword",
                NewPassword = "NewPassword123!"
            };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password" }));

            // Act & Assert
            var act = () => _userService.ChangePasswordAsync(userId, changePasswordDto);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Failed to change password: Incorrect password");
        }

        [Fact]
        public async Task ChangePasswordAsync_WithWeakNewPassword_ShouldThrowException()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser { Id = userId };
            var changePasswordDto = new ChangePasswordDto
            {
                CurrentPassword = "OldPassword123!",
                NewPassword = "weak"
            };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Password must be at least 6 characters" },
                    new IdentityError { Description = "Password must have at least one uppercase character" }));

            // Act & Assert
            var act = () => _userService.ChangePasswordAsync(userId, changePasswordDto);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Failed to change password: Password must be at least 6 characters, Password must have at least one uppercase character");
        }

        #endregion
    }
}
