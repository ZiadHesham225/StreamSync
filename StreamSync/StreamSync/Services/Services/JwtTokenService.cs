using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.Data;
using StreamSync.DTOs;
using StreamSync.Models;

namespace StreamSync.BusinessLogic.Services
{
    public class JwtTokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;

        public JwtTokenService(IConfiguration configuration, IUnitOfWork unitOfWork)
        {
            _configuration = configuration;
            _unitOfWork = unitOfWork;
        }
        public string GenerateAccessToken(IEnumerable<Claim> claims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _configuration["JWT:ValidIssuer"],
                Audience = _configuration["JWT:ValidAudience"],
                Expires = DateTime.UtcNow.AddHours(3),
                SigningCredentials = new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256),
                Subject = new ClaimsIdentity(claims)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"])),
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token");
            }

            return principal;
        }
        public async Task<TokenResponseDto> RefreshAccessTokenAsync(string accessToken, string refreshToken)
        {
            var principal = GetPrincipalFromExpiredToken(accessToken);
            if (principal == null)
            {
                throw new SecurityTokenException("Invalid access token");
            }

            string userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                throw new SecurityTokenException("Invalid access token");
            }

            var storedRefreshToken = await _unitOfWork.RefreshTokens.GetByUserIdAsync(userId);
            if (storedRefreshToken == null ||
                storedRefreshToken.Token != refreshToken ||
                storedRefreshToken.ExpiryTime <= DateTime.UtcNow)
            {
                throw new SecurityTokenException("Invalid refresh token or token expired");
            }
            var newAccessToken = GenerateAccessToken(principal.Claims);
            var newRefreshToken = GenerateRefreshToken();
            var refreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            try
            {
                storedRefreshToken.Token = newRefreshToken;
                storedRefreshToken.ExpiryTime = refreshTokenExpiryTime;
                _unitOfWork.RefreshTokens.Update(storedRefreshToken);
                await _unitOfWork.SaveAsync();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error updating refresh token", ex);
            }
            return new TokenResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                AccessTokenExpiration = DateTime.UtcNow.AddHours(3),
                RefreshTokenExpiration = refreshTokenExpiryTime
            };
        }
        public async Task SaveRefreshTokenAsync(string userId, string token, DateTime expiryTime)
        {
            var existingToken = await _unitOfWork.RefreshTokens.GetByUserIdAsync(userId);

            if (existingToken != null)
            {
                try
                {
                    existingToken.Token = token;
                    existingToken.ExpiryTime = expiryTime;
                    _unitOfWork.RefreshTokens.Update(existingToken);
                    await _unitOfWork.SaveAsync();
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("Error updating refresh token", ex);
                }
            }
            else
            {
                var refreshToken = new RefreshToken
                {
                    UserId = userId,
                    Token = token,
                    ExpiryTime = expiryTime
                };
                try
                {
                    await _unitOfWork.RefreshTokens.CreateAsync(refreshToken);
                    await _unitOfWork.SaveAsync();
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("Error saving refresh token", ex);
                }
            }
        }
        public async Task RevokeRefreshTokenAsync(string userId)
        {
            try
            {
                await _unitOfWork.RefreshTokens.DeleteByUserIdAsync(userId);
                await _unitOfWork.SaveAsync();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error revoking refresh token", ex);
            }
        }
    }
}
