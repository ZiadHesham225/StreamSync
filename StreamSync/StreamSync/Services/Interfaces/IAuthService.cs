using StreamSync.DTOs;

namespace StreamSync.Services.Interfaces
{
    public interface IAuthService
    {
        Task<TokenResponseDto> LoginAsync(LoginDto loginDto);
        Task RegisterUserAsync(RegisterDto registerDto);
        Task<TokenResponseDto> RefreshTokenAsync(string accessToken, string refreshToken);
        Task RevokeTokenAsync(string userId);
        Task ForgotPasswordAsync(ForgotPasswordRequestDto model);
        string GeneratePasswordResetLink(string frontendResetPasswordUrlBase, string token, string userEmail);
        Task ResetPasswordAsync(ResetPasswordRequestDto model);
    }
}
