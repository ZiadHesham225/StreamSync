using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StreamSync.BusinessLogic.Services;
using StreamSync.Data;
using StreamSync.DataAccess.Interfaces;
using StreamSync.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace StreamSync.Tests.Services
{
    public class JwtTokenServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly JwtTokenService _tokenService;
        private const string TestSecret = "ThisIsAVeryLongSecretKeyForTestingPurposes123456789";
        private const string TestIssuer = "TestIssuer";
        private const string TestAudience = "TestAudience";

        public JwtTokenServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();

            SetupConfiguration();

            _tokenService = new JwtTokenService(
                _mockConfiguration.Object,
                _mockUnitOfWork.Object);
        }

        private void SetupConfiguration()
        {
            _mockConfiguration.Setup(c => c["JWT:Secret"]).Returns(TestSecret);
            _mockConfiguration.Setup(c => c["JWT:ValidIssuer"]).Returns(TestIssuer);
            _mockConfiguration.Setup(c => c["JWT:ValidAudience"]).Returns(TestAudience);
        }

        #region GenerateAccessToken Tests

        [Fact]
        public void GenerateAccessToken_WithValidClaims_ShouldReturnToken()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "user-123"),
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Act
            var token = _tokenService.GenerateAccessToken(claims);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            // Verify it's a valid JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            var canRead = tokenHandler.CanReadToken(token);
            canRead.Should().BeTrue();
        }

        [Fact]
        public void GenerateAccessToken_ShouldGenerateTokenWithCorrectClaims()
        {
            // Arrange
            var userId = "user-123";
            var username = "testuser";
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, username)
            };

            // Act
            var token = _tokenService.GenerateAccessToken(claims);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            
            // JWT uses short claim type names (nameid, unique_name) instead of full URIs
            jwtToken.Claims.Should().Contain(c => c.Value == userId);
            jwtToken.Claims.Should().Contain(c => c.Value == username);
        }

        [Fact]
        public void GenerateAccessToken_ShouldSetCorrectIssuerAndAudience()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "user-123")
            };

            // Act
            var token = _tokenService.GenerateAccessToken(claims);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            
            jwtToken.Issuer.Should().Be(TestIssuer);
            jwtToken.Audiences.Should().Contain(TestAudience);
        }

        [Fact]
        public void GenerateAccessToken_ShouldSetExpirationTime()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "user-123")
            };

            // Act
            var token = _tokenService.GenerateAccessToken(claims);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            
            // Token should expire in approximately 3 hours
            jwtToken.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(3), TimeSpan.FromMinutes(5));
        }

        #endregion

        #region GenerateRefreshToken Tests

        [Fact]
        public void GenerateRefreshToken_ShouldReturnNonEmptyString()
        {
            // Act
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Assert
            refreshToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void GenerateRefreshToken_ShouldReturnBase64EncodedString()
        {
            // Act
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Assert
            var isBase64 = () => Convert.FromBase64String(refreshToken);
            isBase64.Should().NotThrow();
        }

        [Fact]
        public void GenerateRefreshToken_ShouldGenerateUniqueTokens()
        {
            // Act
            var tokens = new HashSet<string>();
            for (int i = 0; i < 100; i++)
            {
                tokens.Add(_tokenService.GenerateRefreshToken());
            }

            // Assert
            tokens.Should().HaveCount(100); // All tokens should be unique
        }

        #endregion

        #region GetPrincipalFromExpiredToken Tests

        [Fact]
        public void GetPrincipalFromExpiredToken_WithValidToken_ShouldReturnPrincipal()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "user-123"),
                new Claim(ClaimTypes.Name, "testuser")
            };
            var token = _tokenService.GenerateAccessToken(claims);

            // Act
            var principal = _tokenService.GetPrincipalFromExpiredToken(token);

            // Assert
            principal.Should().NotBeNull();
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("user-123");
            principal.FindFirst(ClaimTypes.Name)?.Value.Should().Be("testuser");
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_WithInvalidToken_ShouldThrowException()
        {
            // Arrange
            var invalidToken = "invalid.jwt.token";

            // Act & Assert
            var act = () => _tokenService.GetPrincipalFromExpiredToken(invalidToken);
            act.Should().Throw<Exception>();
        }

        #endregion

        #region SaveRefreshTokenAsync Tests

        [Fact]
        public async Task SaveRefreshTokenAsync_WithNewUser_ShouldCreateToken()
        {
            // Arrange
            var userId = "user-123";
            var token = "refresh-token";
            var expiryTime = DateTime.UtcNow.AddDays(7);

            var mockRefreshTokenRepo = new Mock<IRefreshTokenRepository>();
            mockRefreshTokenRepo.Setup(r => r.GetByUserIdAsync(userId))
                .ReturnsAsync((RefreshToken?)null);
            mockRefreshTokenRepo.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>()))
                .ReturnsAsync((RefreshToken rt) => rt);
            
            _mockUnitOfWork.Setup(u => u.RefreshTokens).Returns(mockRefreshTokenRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            await _tokenService.SaveRefreshTokenAsync(userId, token, expiryTime);

            // Assert
            mockRefreshTokenRepo.Verify(r => r.CreateAsync(It.Is<RefreshToken>(rt => 
                rt.UserId == userId && 
                rt.Token == token && 
                rt.ExpiryTime == expiryTime)), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task SaveRefreshTokenAsync_WithExistingUser_ShouldUpdateToken()
        {
            // Arrange
            var userId = "user-123";
            var newToken = "new-refresh-token";
            var newExpiryTime = DateTime.UtcNow.AddDays(7);
            var existingToken = new RefreshToken
            {
                Id = 1,
                UserId = userId,
                Token = "old-token",
                ExpiryTime = DateTime.UtcNow.AddDays(1)
            };

            var mockRefreshTokenRepo = new Mock<IRefreshTokenRepository>();
            mockRefreshTokenRepo.Setup(r => r.GetByUserIdAsync(userId))
                .ReturnsAsync(existingToken);
            
            _mockUnitOfWork.Setup(u => u.RefreshTokens).Returns(mockRefreshTokenRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            await _tokenService.SaveRefreshTokenAsync(userId, newToken, newExpiryTime);

            // Assert
            existingToken.Token.Should().Be(newToken);
            existingToken.ExpiryTime.Should().Be(newExpiryTime);
            mockRefreshTokenRepo.Verify(r => r.Update(existingToken), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        #endregion

        #region RevokeRefreshTokenAsync Tests

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldDeleteToken()
        {
            // Arrange
            var userId = "user-123";

            var mockRefreshTokenRepo = new Mock<IRefreshTokenRepository>();
            mockRefreshTokenRepo.Setup(r => r.DeleteByUserIdAsync(userId))
                .Returns(Task.CompletedTask);
            
            _mockUnitOfWork.Setup(u => u.RefreshTokens).Returns(mockRefreshTokenRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            await _tokenService.RevokeRefreshTokenAsync(userId);

            // Assert
            mockRefreshTokenRepo.Verify(r => r.DeleteByUserIdAsync(userId), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        #endregion

        #region RefreshAccessTokenAsync Tests

        [Fact]
        public async Task RefreshAccessTokenAsync_WithValidTokens_ShouldReturnNewTokens()
        {
            // Arrange
            var userId = "user-123";
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "testuser")
            };
            var accessToken = _tokenService.GenerateAccessToken(claims);
            var refreshToken = "valid-refresh-token";

            var storedToken = new RefreshToken
            {
                Id = 1,
                UserId = userId,
                Token = refreshToken,
                ExpiryTime = DateTime.UtcNow.AddDays(7)
            };

            var mockRefreshTokenRepo = new Mock<IRefreshTokenRepository>();
            mockRefreshTokenRepo.Setup(r => r.GetByUserIdAsync(userId))
                .ReturnsAsync(storedToken);
            
            _mockUnitOfWork.Setup(u => u.RefreshTokens).Returns(mockRefreshTokenRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _tokenService.RefreshAccessTokenAsync(accessToken, refreshToken);

            // Assert
            result.Should().NotBeNull();
            result.AccessToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBe(refreshToken); // New refresh token generated
        }

        [Fact]
        public async Task RefreshAccessTokenAsync_WithExpiredRefreshToken_ShouldThrowException()
        {
            // Arrange
            var userId = "user-123";
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var accessToken = _tokenService.GenerateAccessToken(claims);
            var refreshToken = "expired-refresh-token";

            var storedToken = new RefreshToken
            {
                Id = 1,
                UserId = userId,
                Token = refreshToken,
                ExpiryTime = DateTime.UtcNow.AddDays(-1) // Expired
            };

            var mockRefreshTokenRepo = new Mock<IRefreshTokenRepository>();
            mockRefreshTokenRepo.Setup(r => r.GetByUserIdAsync(userId))
                .ReturnsAsync(storedToken);
            
            _mockUnitOfWork.Setup(u => u.RefreshTokens).Returns(mockRefreshTokenRepo.Object);

            // Act & Assert
            var act = () => _tokenService.RefreshAccessTokenAsync(accessToken, refreshToken);
            await act.Should().ThrowAsync<SecurityTokenException>()
                .WithMessage("*Invalid refresh token or token expired*");
        }

        [Fact]
        public async Task RefreshAccessTokenAsync_WithInvalidRefreshToken_ShouldThrowException()
        {
            // Arrange
            var userId = "user-123";
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var accessToken = _tokenService.GenerateAccessToken(claims);
            var refreshToken = "wrong-refresh-token";

            var storedToken = new RefreshToken
            {
                Id = 1,
                UserId = userId,
                Token = "different-token",
                ExpiryTime = DateTime.UtcNow.AddDays(7)
            };

            var mockRefreshTokenRepo = new Mock<IRefreshTokenRepository>();
            mockRefreshTokenRepo.Setup(r => r.GetByUserIdAsync(userId))
                .ReturnsAsync(storedToken);
            
            _mockUnitOfWork.Setup(u => u.RefreshTokens).Returns(mockRefreshTokenRepo.Object);

            // Act & Assert
            var act = () => _tokenService.RefreshAccessTokenAsync(accessToken, refreshToken);
            await act.Should().ThrowAsync<SecurityTokenException>();
        }

        #endregion
    }
}
