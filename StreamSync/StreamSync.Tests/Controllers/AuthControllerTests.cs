using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.BusinessLogic.Services;
using StreamSync.Controllers;
using StreamSync.DTOs;
using System.Security.Claims;

namespace StreamSync.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly Mock<ILogger<AuthController>> _mockLogger;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockAuthService = new Mock<IAuthService>();
            _mockLogger = new Mock<ILogger<AuthController>>();

            _controller = new AuthController(
                _mockAuthService.Object,
                _mockLogger.Object);
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

        #region Register Tests

        [Fact]
        public async Task Register_WithValidData_ShouldReturn201Created()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Username = "newuser",
                Email = "newuser@example.com",
                Password = "Password123!",
                DisplayName = "New User"
            };

            _mockAuthService.Setup(s => s.RegisterUserAsync(registerDto))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Register(registerDto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        }

        [Fact]
        public async Task Register_WithExistingUser_ShouldReturnBadRequest()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Username = "existinguser",
                Email = "existing@example.com",
                Password = "Password123!",
                DisplayName = "Existing User"
            };

            _mockAuthService.Setup(s => s.RegisterUserAsync(registerDto))
                .ThrowsAsync(new AuthenticationException("User with this username already exists"));

            // Act
            var result = await _controller.Register(registerDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        }

        [Fact]
        public async Task Register_WhenExceptionOccurs_ShouldReturn500()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Username = "newuser",
                Email = "new@example.com",
                Password = "Password123!",
                DisplayName = "New User"
            };

            _mockAuthService.Setup(s => s.RegisterUserAsync(registerDto))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Register(registerDto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        }

        #endregion

        #region Login Tests

        [Fact]
        public async Task Login_WithValidCredentials_ShouldReturnTokens()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "test@example.com",
                Password = "Password123!"
            };

            var expectedResponse = new TokenResponseDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                AccessTokenExpiration = DateTime.UtcNow.AddHours(3),
                RefreshTokenExpiration = DateTime.UtcNow.AddDays(7)
            };

            _mockAuthService.Setup(s => s.LoginAsync(loginDto))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.Login(loginDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<TokenResponseDto>().Subject;
            response.AccessToken.Should().Be("access-token");
            response.RefreshToken.Should().Be("refresh-token");
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ShouldReturnBadRequest()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "test@example.com",
                Password = "WrongPassword"
            };

            _mockAuthService.Setup(s => s.LoginAsync(loginDto))
                .ThrowsAsync(new AuthenticationException("Invalid email or password"));

            // Act
            var result = await _controller.Login(loginDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        }

        [Fact]
        public async Task Login_WhenExceptionOccurs_ShouldReturn500()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "test@example.com",
                Password = "Password123!"
            };

            _mockAuthService.Setup(s => s.LoginAsync(loginDto))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Login(loginDto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        }

        #endregion

        #region RefreshToken Tests

        [Fact]
        public async Task RefreshToken_WithValidTokens_ShouldReturnNewTokens()
        {
            // Arrange
            var refreshTokenDto = new RefreshTokenDto
            {
                AccessToken = "old-access-token",
                RefreshToken = "old-refresh-token"
            };

            var expectedResponse = new TokenResponseDto
            {
                AccessToken = "new-access-token",
                RefreshToken = "new-refresh-token",
                AccessTokenExpiration = DateTime.UtcNow.AddHours(3),
                RefreshTokenExpiration = DateTime.UtcNow.AddDays(7)
            };

            _mockAuthService.Setup(s => s.RefreshTokenAsync(
                    refreshTokenDto.AccessToken,
                    refreshTokenDto.RefreshToken))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.RefreshToken(refreshTokenDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<TokenResponseDto>().Subject;
            response.AccessToken.Should().Be("new-access-token");
            response.RefreshToken.Should().Be("new-refresh-token");
        }

        [Fact]
        public async Task RefreshToken_WithInvalidToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var refreshTokenDto = new RefreshTokenDto
            {
                AccessToken = "invalid-token",
                RefreshToken = "invalid-refresh-token"
            };

            _mockAuthService.Setup(s => s.RefreshTokenAsync(
                    refreshTokenDto.AccessToken,
                    refreshTokenDto.RefreshToken))
                .ThrowsAsync(new SecurityTokenException("Invalid token"));

            // Act
            var result = await _controller.RefreshToken(refreshTokenDto);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public async Task RefreshToken_WhenExceptionOccurs_ShouldReturn500()
        {
            // Arrange
            var refreshTokenDto = new RefreshTokenDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token"
            };

            _mockAuthService.Setup(s => s.RefreshTokenAsync(
                    refreshTokenDto.AccessToken,
                    refreshTokenDto.RefreshToken))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.RefreshToken(refreshTokenDto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        }

        #endregion

        #region RevokeToken Tests

        [Fact]
        public async Task RevokeToken_WithAuthenticatedUser_ShouldReturnOk()
        {
            // Arrange
            var userId = "user-123";
            SetupUserContext(userId, "testuser");

            _mockAuthService.Setup(s => s.RevokeTokenAsync(userId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.RevokeToken();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        }

        [Fact]
        public async Task RevokeToken_WithoutUserId_ShouldReturnUnauthorized()
        {
            // Arrange - set up context with no user ID
            var claims = new List<Claim>();
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Act
            var result = await _controller.RevokeToken();

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        #endregion
    }
}
