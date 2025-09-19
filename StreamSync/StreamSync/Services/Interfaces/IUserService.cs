using StreamSync.DTOs;

namespace StreamSync.BusinessLogic.Interfaces
{
    public interface IUserService
    {
        Task<UserProfileDto> GetUserProfileAsync(string userId);
        Task<UserProfileDto> UpdateUserProfileAsync(string userId, UpdateUserProfileDto updateProfileDto);
        Task ChangePasswordAsync(string userId, ChangePasswordDto changePasswordDto);
    }
}