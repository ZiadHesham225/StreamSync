using Microsoft.AspNetCore.Identity;
using StreamSync.Services.Interfaces;
using StreamSync.DTOs;
using StreamSync.Models;

namespace StreamSync.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UserService> _logger;

        public UserService(UserManager<ApplicationUser> userManager, ILogger<UserService> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<UserProfileDto> GetUserProfileAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    throw new InvalidOperationException("User not found");
                }

                return new UserProfileDto
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    DisplayName = user.DisplayName ?? string.Empty,
                    AvatarUrl = user.AvatarUrl,
                    CreatedAt = user.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user profile for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<UserProfileDto> UpdateUserProfileAsync(string userId, UpdateUserProfileDto updateProfileDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    throw new InvalidOperationException("User not found");
                }

                user.DisplayName = updateProfileDto.DisplayName;
                user.AvatarUrl = updateProfileDto.AvatarUrl;

                var result = await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning("Failed to update user profile for user: {UserId}. Errors: {Errors}", userId, errors);
                    throw new InvalidOperationException($"Failed to update profile: {errors}");
                }

                _logger.LogInformation("User profile updated successfully for user: {UserId}", userId);

                return new UserProfileDto
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    DisplayName = user.DisplayName ?? string.Empty,
                    AvatarUrl = user.AvatarUrl,
                    CreatedAt = user.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile for user: {UserId}", userId);
                throw;
            }
        }

        public async Task ChangePasswordAsync(string userId, ChangePasswordDto changePasswordDto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    throw new InvalidOperationException("User not found");
                }

                var result = await _userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning("Failed to change password for user: {UserId}. Errors: {Errors}", userId, errors);
                    throw new InvalidOperationException($"Failed to change password: {errors}");
                }

                _logger.LogInformation("Password changed successfully for user: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserId}", userId);
                throw;
            }
        }
    }
}