using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Web;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.DTOs;
using StreamSync.Models;

namespace StreamSync.BusinessLogic.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;
        public AuthService(UserManager<ApplicationUser> userManager, IConfiguration configuration, IEmailService emailService, ITokenService tokenService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _emailService = emailService;
            _tokenService = tokenService;
        }
        public async Task ForgotPasswordAsync(ForgotPasswordRequestDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                if (!string.IsNullOrEmpty(token))
                {
                    var frontendUrl = _configuration["Frontend:ResetPasswordUrl"] ?? _configuration["JWT:ValidIssuer"];
                    var link = GeneratePasswordResetLink(frontendUrl, token, model.Email);

                    if (!string.IsNullOrEmpty(link))
                    {
                        await _emailService.SendPasswordResetEmailAsync(model.Email, user.UserName, link);
                    }
                }
            }
        }

        public string GeneratePasswordResetLink(string frontendResetPasswordUrlBase, string token, string userEmail)
        {
            if (frontendResetPasswordUrlBase == null)
            {
                return string.Empty;
            }
            var encodedToken = HttpUtility.UrlEncode(token);

            var resetLink = $"{frontendResetPasswordUrlBase}?email={HttpUtility.UrlEncode(userEmail)}&token={encodedToken}";

            return resetLink;
        }
        public async Task ResetPasswordAsync(ResetPasswordRequestDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null) throw new ApplicationException(message: $"Email: {model.Email} Not Found!");

            var decodedToken = Uri.UnescapeDataString(model.Token);

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ApplicationException($"Failed to reset password. Errors: {errors}");
            }
        }
        public async Task<TokenResponseDto> LoginAsync(LoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email);
            if (user == null)
                throw new AuthenticationException("Invalid email or password");

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);
            if (!isPasswordValid)
                throw new AuthenticationException("Invalid email or password");

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var tokens = await GenerateUserTokensAsync(user);

            return new TokenResponseDto
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                AccessTokenExpiration = tokens.AccessTokenExpiration,
                RefreshTokenExpiration = tokens.RefreshTokenExpiration
            };
        }

        public async Task RegisterUserAsync(RegisterDto registerDto)
        {
            var userExists = await _userManager.FindByNameAsync(registerDto.Username);
            if (userExists != null)
                throw new AuthenticationException("User with this username already exists");

            var emailExists = await _userManager.FindByEmailAsync(registerDto.Email);
            if (emailExists != null)
                throw new AuthenticationException("User with this email already exists");

            var user = new ApplicationUser
            {
                DisplayName = registerDto.DisplayName,
                UserName = registerDto.Username,
                Email = registerDto.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new AuthenticationException($"User registration failed: {errors}");
            }
        }

        public async Task<TokenResponseDto> RefreshTokenAsync(string accessToken, string refreshToken)
        {
            var tokenModel = await _tokenService.RefreshAccessTokenAsync(accessToken, refreshToken);

            return new TokenResponseDto
            {
                AccessToken = tokenModel.AccessToken,
                RefreshToken = tokenModel.RefreshToken,
                AccessTokenExpiration = tokenModel.AccessTokenExpiration,
                RefreshTokenExpiration = tokenModel.RefreshTokenExpiration
            };
        }
        public async Task RevokeTokenAsync(string userId)
        {
            await _tokenService.RevokeRefreshTokenAsync(userId);
        }
        private async Task<TokenResponseDto> GenerateUserTokensAsync(ApplicationUser user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var userRole in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, userRole));
            }

            var accessToken = _tokenService.GenerateAccessToken(authClaims);
            var refreshToken = _tokenService.GenerateRefreshToken();
            var refreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            await _tokenService.SaveRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiryTime);

            return new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiration = DateTime.UtcNow.AddHours(3),
                RefreshTokenExpiration = refreshTokenExpiryTime
            };
        }
    }
    public class AuthenticationException : Exception
    {
        public AuthenticationException(string message) : base(message)
        {
        }
    }
}
