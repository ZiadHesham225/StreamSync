using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.BusinessLogic.Services;
using StreamSync.DTOs;
using StreamSync.Models;

namespace StreamSync.Tests.Services
{
    public class AuthServiceTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _mockUserManager = CreateMockUserManager();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockTokenService = new Mock<ITokenService>();
            _mockEmailService = new Mock<IEmailService>();

            _authService = new AuthService(
                _mockUserManager.Object,
                _mockConfiguration.Object,
                _mockEmailService.Object,
                _mockTokenService.Object);
        }

        private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        #region LoginAsync Tests

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ShouldReturnTokenResponse()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "test@example.com",
                Password = "Password123!"
            };

            var user = new ApplicationUser
            {
                Id = "user-123",
                Email = loginDto.Email,
                UserName = "testuser"
            };

            _mockUserManager.Setup(m => m.FindByEmailAsync(loginDto.Email))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.CheckPasswordAsync(user, loginDto.Password))
                .ReturnsAsync(true);
            _mockUserManager.Setup(m => m.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string>());

            _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<IEnumerable<System.Security.Claims.Claim>>()))
                .Returns("access-token");
            _mockTokenService.Setup(t => t.GenerateRefreshToken())
                .Returns("refresh-token");
            _mockTokenService.Setup(t => t.SaveRefreshTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _authService.LoginAsync(loginDto);

            // Assert
            result.Should().NotBeNull();
            result.AccessToken.Should().Be("access-token");
            result.RefreshToken.Should().Be("refresh-token");
        }

        [Fact]
        public async Task LoginAsync_WithInvalidEmail_ShouldThrowAuthenticationException()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "nonexistent@example.com",
                Password = "Password123!"
            };

            _mockUserManager.Setup(m => m.FindByEmailAsync(loginDto.Email))
                .ReturnsAsync((ApplicationUser?)null);

            // Act & Assert
            var act = () => _authService.LoginAsync(loginDto);
            await act.Should().ThrowAsync<AuthenticationException>()
                .WithMessage("Invalid email or password");
        }

        [Fact]
        public async Task LoginAsync_WithInvalidPassword_ShouldThrowAuthenticationException()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "test@example.com",
                Password = "WrongPassword"
            };

            var user = new ApplicationUser
            {
                Id = "user-123",
                Email = loginDto.Email
            };

            _mockUserManager.Setup(m => m.FindByEmailAsync(loginDto.Email))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.CheckPasswordAsync(user, loginDto.Password))
                .ReturnsAsync(false);

            // Act & Assert
            var act = () => _authService.LoginAsync(loginDto);
            await act.Should().ThrowAsync<AuthenticationException>()
                .WithMessage("Invalid email or password");
        }

        [Fact]
        public async Task LoginAsync_ShouldUpdateLastLoginTime()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "test@example.com",
                Password = "Password123!"
            };

            var user = new ApplicationUser
            {
                Id = "user-123",
                Email = loginDto.Email,
                UserName = "testuser",
                LastLoginAt = DateTime.UtcNow.AddDays(-1)
            };

            _mockUserManager.Setup(m => m.FindByEmailAsync(loginDto.Email))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.CheckPasswordAsync(user, loginDto.Password))
                .ReturnsAsync(true);
            _mockUserManager.Setup(m => m.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string>());

            _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<IEnumerable<System.Security.Claims.Claim>>()))
                .Returns("access-token");
            _mockTokenService.Setup(t => t.GenerateRefreshToken())
                .Returns("refresh-token");
            _mockTokenService.Setup(t => t.SaveRefreshTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .Returns(Task.CompletedTask);

            // Act
            await _authService.LoginAsync(loginDto);

            // Assert
            _mockUserManager.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u => 
                u.LastLoginAt > DateTime.UtcNow.AddMinutes(-1))), Times.Once);
        }

        #endregion

        #region RegisterUserAsync Tests

        [Fact]
        public async Task RegisterUserAsync_WithValidData_ShouldCreateUser()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Username = "newuser",
                Email = "newuser@example.com",
                Password = "Password123!",
                DisplayName = "New User"
            };

            _mockUserManager.Setup(m => m.FindByNameAsync(registerDto.Username))
                .ReturnsAsync((ApplicationUser?)null);
            _mockUserManager.Setup(m => m.FindByEmailAsync(registerDto.Email))
                .ReturnsAsync((ApplicationUser?)null);
            _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), registerDto.Password))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            await _authService.RegisterUserAsync(registerDto);

            // Assert
            _mockUserManager.Verify(m => m.CreateAsync(
                It.Is<ApplicationUser>(u => 
                    u.UserName == registerDto.Username && 
                    u.Email == registerDto.Email &&
                    u.DisplayName == registerDto.DisplayName),
                registerDto.Password), Times.Once);
        }

        [Fact]
        public async Task RegisterUserAsync_WithExistingUsername_ShouldThrowAuthenticationException()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Username = "existinguser",
                Email = "new@example.com",
                Password = "Password123!",
                DisplayName = "Test User"
            };

            var existingUser = new ApplicationUser { UserName = registerDto.Username };
            _mockUserManager.Setup(m => m.FindByNameAsync(registerDto.Username))
                .ReturnsAsync(existingUser);

            // Act & Assert
            var act = () => _authService.RegisterUserAsync(registerDto);
            await act.Should().ThrowAsync<AuthenticationException>()
                .WithMessage("User with this username already exists");
        }

        [Fact]
        public async Task RegisterUserAsync_WithExistingEmail_ShouldThrowAuthenticationException()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Username = "newuser",
                Email = "existing@example.com",
                Password = "Password123!",
                DisplayName = "Test User"
            };

            var existingUser = new ApplicationUser { Email = registerDto.Email };
            _mockUserManager.Setup(m => m.FindByNameAsync(registerDto.Username))
                .ReturnsAsync((ApplicationUser?)null);
            _mockUserManager.Setup(m => m.FindByEmailAsync(registerDto.Email))
                .ReturnsAsync(existingUser);

            // Act & Assert
            var act = () => _authService.RegisterUserAsync(registerDto);
            await act.Should().ThrowAsync<AuthenticationException>()
                .WithMessage("User with this email already exists");
        }

        [Fact]
        public async Task RegisterUserAsync_WithFailedCreation_ShouldThrowAuthenticationException()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Username = "newuser",
                Email = "new@example.com",
                Password = "weak",
                DisplayName = "Test User"
            };

            _mockUserManager.Setup(m => m.FindByNameAsync(registerDto.Username))
                .ReturnsAsync((ApplicationUser?)null);
            _mockUserManager.Setup(m => m.FindByEmailAsync(registerDto.Email))
                .ReturnsAsync((ApplicationUser?)null);
            _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), registerDto.Password))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

            // Act & Assert
            var act = () => _authService.RegisterUserAsync(registerDto);
            await act.Should().ThrowAsync<AuthenticationException>()
                .WithMessage("User registration failed: Password too weak");
        }

        #endregion

        #region RefreshTokenAsync Tests

        [Fact]
        public async Task RefreshTokenAsync_WithValidTokens_ShouldReturnNewTokens()
        {
            // Arrange
            var accessToken = "old-access-token";
            var refreshToken = "old-refresh-token";
            var expectedResponse = new TokenResponseDto
            {
                AccessToken = "new-access-token",
                RefreshToken = "new-refresh-token",
                AccessTokenExpiration = DateTime.UtcNow.AddHours(3),
                RefreshTokenExpiration = DateTime.UtcNow.AddDays(7)
            };

            _mockTokenService.Setup(t => t.RefreshAccessTokenAsync(accessToken, refreshToken))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _authService.RefreshTokenAsync(accessToken, refreshToken);

            // Assert
            result.Should().NotBeNull();
            result.AccessToken.Should().Be("new-access-token");
            result.RefreshToken.Should().Be("new-refresh-token");
        }

        #endregion

        #region RevokeTokenAsync Tests

        [Fact]
        public async Task RevokeTokenAsync_ShouldCallTokenService()
        {
            // Arrange
            var userId = "user-123";
            _mockTokenService.Setup(t => t.RevokeRefreshTokenAsync(userId))
                .Returns(Task.CompletedTask);

            // Act
            await _authService.RevokeTokenAsync(userId);

            // Assert
            _mockTokenService.Verify(t => t.RevokeRefreshTokenAsync(userId), Times.Once);
        }

        #endregion

        #region ForgotPasswordAsync Tests

        [Fact]
        public async Task ForgotPasswordAsync_WithExistingUser_ShouldSendEmail()
        {
            // Arrange
            var model = new ForgotPasswordRequestDto { Email = "test@example.com" };
            var user = new ApplicationUser
            {
                Email = model.Email,
                UserName = "testuser"
            };
            var resetToken = "reset-token";
            var frontendUrl = "https://example.com/reset-password";

            _mockUserManager.Setup(m => m.FindByEmailAsync(model.Email))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.GeneratePasswordResetTokenAsync(user))
                .ReturnsAsync(resetToken);

            var mockConfigSection = new Mock<IConfigurationSection>();
            mockConfigSection.Setup(s => s.Value).Returns(frontendUrl);
            _mockConfiguration.Setup(c => c["Frontend:ResetPasswordUrl"])
                .Returns(frontendUrl);

            _mockEmailService.Setup(e => e.SendPasswordResetEmailAsync(
                    model.Email, 
                    user.UserName, 
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _authService.ForgotPasswordAsync(model);

            // Assert
            _mockEmailService.Verify(e => e.SendPasswordResetEmailAsync(
                model.Email,
                user.UserName,
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WithNonExistentUser_ShouldNotSendEmail()
        {
            // Arrange
            var model = new ForgotPasswordRequestDto { Email = "nonexistent@example.com" };

            _mockUserManager.Setup(m => m.FindByEmailAsync(model.Email))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            await _authService.ForgotPasswordAsync(model);

            // Assert
            _mockEmailService.Verify(e => e.SendPasswordResetEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region ResetPasswordAsync Tests

        [Fact]
        public async Task ResetPasswordAsync_WithValidToken_ShouldResetPassword()
        {
            // Arrange
            var model = new ResetPasswordRequestDto
            {
                Email = "test@example.com",
                Token = "valid-token",
                NewPassword = "NewPassword123!"
            };

            var user = new ApplicationUser { Email = model.Email };

            _mockUserManager.Setup(m => m.FindByEmailAsync(model.Email))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.ResetPasswordAsync(user, It.IsAny<string>(), model.NewPassword))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            await _authService.ResetPasswordAsync(model);

            // Assert
            _mockUserManager.Verify(m => m.ResetPasswordAsync(user, It.IsAny<string>(), model.NewPassword), Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_WithNonExistentUser_ShouldThrowException()
        {
            // Arrange
            var model = new ResetPasswordRequestDto
            {
                Email = "nonexistent@example.com",
                Token = "valid-token",
                NewPassword = "NewPassword123!"
            };

            _mockUserManager.Setup(m => m.FindByEmailAsync(model.Email))
                .ReturnsAsync((ApplicationUser?)null);

            // Act & Assert
            var act = () => _authService.ResetPasswordAsync(model);
            await act.Should().ThrowAsync<ApplicationException>()
                .WithMessage($"Email: {model.Email} Not Found!");
        }

        [Fact]
        public async Task ResetPasswordAsync_WithInvalidToken_ShouldThrowException()
        {
            // Arrange
            var model = new ResetPasswordRequestDto
            {
                Email = "test@example.com",
                Token = "invalid-token",
                NewPassword = "NewPassword123!"
            };

            var user = new ApplicationUser { Email = model.Email };

            _mockUserManager.Setup(m => m.FindByEmailAsync(model.Email))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.ResetPasswordAsync(user, It.IsAny<string>(), model.NewPassword))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

            // Act & Assert
            var act = () => _authService.ResetPasswordAsync(model);
            await act.Should().ThrowAsync<ApplicationException>()
                .WithMessage("Failed to reset password. Errors: Invalid token");
        }

        #endregion

        #region GeneratePasswordResetLink Tests

        [Fact]
        public void GeneratePasswordResetLink_WithValidParams_ShouldReturnLink()
        {
            // Arrange
            var frontendUrl = "https://example.com/reset-password";
            var token = "reset-token";
            var email = "test@example.com";

            // Act
            var result = _authService.GeneratePasswordResetLink(frontendUrl, token, email);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain(frontendUrl);
            result.Should().Contain("email=");
            result.Should().Contain("token=");
        }

        [Fact]
        public void GeneratePasswordResetLink_WithNullFrontendUrl_ShouldReturnEmptyString()
        {
            // Arrange
            string? frontendUrl = null;
            var token = "reset-token";
            var email = "test@example.com";

            // Act
            var result = _authService.GeneratePasswordResetLink(frontendUrl!, token, email);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion
    }
}
