using StreamSync.DataAccess.Interfaces;
using StreamSync.DTOs;
using StreamSync.Services.Interfaces;

namespace StreamSync.Services.Decorators
{
    /// <summary>
    /// Caching decorator for IUserService.
    /// Caches user profile data to reduce database queries.
    /// </summary>
    public class UserServiceCachingDecorator : IUserService
    {
        private readonly IUserService _inner;
        private readonly ICacheService _cache;
        private readonly ILogger<UserServiceCachingDecorator> _logger;

        public UserServiceCachingDecorator(
            IUserService inner,
            ICacheService cache,
            ILogger<UserServiceCachingDecorator> logger)
        {
            _inner = inner;
            _cache = cache;
            _logger = logger;
        }

        public async Task<UserProfileDto> GetUserProfileAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new InvalidOperationException("User ID is required");
            }

            var cacheKey = CacheKeys.Generate(CacheKeys.UserProfile, userId);

            var cached = await _cache.GetAsync<UserProfileDto>(cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("User profile cache hit for user: {UserId}", userId);
                return cached;
            }

            _logger.LogDebug("User profile cache miss for user: {UserId}", userId);
            var profile = await _inner.GetUserProfileAsync(userId);
            
            await _cache.SetAsync(cacheKey, profile, CacheDurations.UserProfile);
            
            return profile;
        }

        public async Task<UserProfileDto> UpdateUserProfileAsync(string userId, UpdateUserProfileDto updateProfileDto)
        {
            var profile = await _inner.UpdateUserProfileAsync(userId, updateProfileDto);
            
            // Invalidate the cached profile
            var cacheKey = CacheKeys.Generate(CacheKeys.UserProfile, userId);
            await _cache.RemoveAsync(cacheKey);
            
            // Cache the updated profile
            await _cache.SetAsync(cacheKey, profile, CacheDurations.UserProfile);
            
            _logger.LogDebug("Updated and recached user profile for user: {UserId}", userId);
            
            return profile;
        }

        public async Task ChangePasswordAsync(string userId, ChangePasswordDto changePasswordDto)
        {
            // Password change doesn't affect cached profile data
            // Just pass through to the inner service
            await _inner.ChangePasswordAsync(userId, changePasswordDto);
        }
    }
}
