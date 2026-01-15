using StreamSync.DataAccess.Interfaces;
using StreamSync.DTOs;
using StreamSync.Models;
using StreamSync.Services.Interfaces;

namespace StreamSync.Services.Decorators
{
    /// <summary>
    /// Caching decorator for IRoomService.
    /// Implements the decorator pattern to add Redis caching without modifying the original service.
    /// </summary>
    public class RoomServiceCachingDecorator : IRoomService
    {
        private readonly IRoomService _inner;
        private readonly ICacheService _cache;
        private readonly ILogger<RoomServiceCachingDecorator> _logger;

        public RoomServiceCachingDecorator(
            IRoomService inner,
            ICacheService cache,
            ILogger<RoomServiceCachingDecorator> logger)
        {
            _inner = inner;
            _cache = cache;
            _logger = logger;
        }

        public async Task<Room?> GetRoomByIdAsync(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return null;

            var cacheKey = CacheKeys.Generate(CacheKeys.RoomById, roomId);

            return await _cache.GetOrSetAsync(
                cacheKey,
                () => _inner.GetRoomByIdAsync(roomId),
                CacheDurations.RoomById);
        }

        public async Task<Room?> GetRoomByInviteCodeAsync(string inviteCode)
        {
            if (string.IsNullOrWhiteSpace(inviteCode))
                return null;

            var cacheKey = CacheKeys.Generate(CacheKeys.RoomByInviteCode, inviteCode);

            return await _cache.GetOrSetAsync(
                cacheKey,
                () => _inner.GetRoomByInviteCodeAsync(inviteCode),
                CacheDurations.RoomByInviteCode);
        }

        public async Task<IEnumerable<RoomDto>> GetActiveRoomsAsync()
        {
            var cacheKey = CacheKeys.ActiveRooms;

            var cached = await _cache.GetAsync<List<RoomDto>>(cacheKey);
            if (cached != null)
                return cached;

            var result = (await _inner.GetActiveRoomsAsync()).ToList();
            await _cache.SetAsync(cacheKey, result, CacheDurations.ActiveRooms);
            return result;
        }

        public async Task<PagedResultDto<RoomDto>> GetActiveRoomsAsync(PaginationQueryDto pagination)
        {
            var cacheKey = CacheKeys.Generate(
                CacheKeys.ActiveRoomsPaged,
                pagination.Page,
                pagination.PageSize,
                pagination.SortBy ?? "createdat",
                pagination.SortOrder ?? "desc",
                pagination.Search ?? "");

            var cached = await _cache.GetAsync<PagedResultDto<RoomDto>>(cacheKey);
            if (cached != null)
                return cached;

            var result = await _inner.GetActiveRoomsAsync(pagination);
            await _cache.SetAsync(cacheKey, result, CacheDurations.ActiveRooms);
            return result;
        }

        public async Task<IEnumerable<RoomDto>> GetUserRoomsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Enumerable.Empty<RoomDto>();

            var cacheKey = CacheKeys.Generate(CacheKeys.UserRooms, userId);

            var cached = await _cache.GetAsync<List<RoomDto>>(cacheKey);
            if (cached != null)
                return cached;

            var result = (await _inner.GetUserRoomsAsync(userId)).ToList();
            await _cache.SetAsync(cacheKey, result, CacheDurations.UserRooms);
            return result;
        }

        public async Task<PagedResultDto<RoomDto>> GetUserRoomsAsync(string userId, PaginationQueryDto pagination)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return new PagedResultDto<RoomDto>();

            var cacheKey = CacheKeys.Generate(
                CacheKeys.UserRoomsPaged,
                userId,
                pagination.Page,
                pagination.PageSize,
                pagination.SortBy ?? "createdat",
                pagination.SortOrder ?? "desc",
                pagination.Search ?? "");

            var cached = await _cache.GetAsync<PagedResultDto<RoomDto>>(cacheKey);
            if (cached != null)
                return cached;

            var result = await _inner.GetUserRoomsAsync(userId, pagination);
            await _cache.SetAsync(cacheKey, result, CacheDurations.UserRooms);
            return result;
        }

        public async Task<bool> IsUserAdminAsync(string roomId, string userId)
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId))
                return false;

            var cacheKey = CacheKeys.Generate(CacheKeys.RoomAdminCheck, roomId, userId);

            // For boolean results, wrap in a class since GetOrSetAsync requires class constraint
            var cached = await _cache.GetAsync<CachedBool>(cacheKey);
            if (cached != null)
                return cached.Value;

            var result = await _inner.IsUserAdminAsync(roomId, userId);
            await _cache.SetAsync(cacheKey, new CachedBool { Value = result }, CacheDurations.RoomPermissionCheck);
            return result;
        }

        public async Task<bool> CanUserControlRoomAsync(string roomId, string userId)
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId))
                return false;

            var cacheKey = CacheKeys.Generate(CacheKeys.RoomControlCheck, roomId, userId);

            var cached = await _cache.GetAsync<CachedBool>(cacheKey);
            if (cached != null)
                return cached.Value;

            var result = await _inner.CanUserControlRoomAsync(roomId, userId);
            await _cache.SetAsync(cacheKey, new CachedBool { Value = result }, CacheDurations.RoomPermissionCheck);
            return result;
        }

        #region Write Operations (with cache invalidation)

        public async Task<Room?> CreateRoomAsync(RoomCreateDto roomDto, string userId)
        {
            var room = await _inner.CreateRoomAsync(roomDto, userId);
            
            if (room != null)
            {
                // Invalidate active rooms list cache
                await _cache.RemoveAsync(CacheKeys.ActiveRooms);
                await _cache.RemoveByPatternAsync("rooms:active:page:*");
                
                // Invalidate user's rooms cache
                await _cache.RemoveAsync(CacheKeys.Generate(CacheKeys.UserRooms, userId));
                await _cache.RemoveByPatternAsync($"rooms:user:{userId.ToLowerInvariant()}:page:*");
                
                _logger.LogDebug("Invalidated room caches after creating room {RoomId}", room.Id);
            }
            
            return room;
        }

        public async Task<bool> UpdateRoomAsync(RoomUpdateDto roomDto, string userId)
        {
            var result = await _inner.UpdateRoomAsync(roomDto, userId);
            
            if (result && roomDto.RoomId != null)
            {
                await InvalidateRoomCacheAsync(roomDto.RoomId, userId);
            }
            
            return result;
        }

        public async Task<bool> UpdateRoomVideoAsync(string roomId, string videoUrl)
        {
            var result = await _inner.UpdateRoomVideoAsync(roomId, videoUrl);
            
            if (result)
            {
                // Just invalidate the room itself
                await _cache.RemoveAsync(CacheKeys.Generate(CacheKeys.RoomById, roomId));
            }
            
            return result;
        }

        public async Task<bool> EndRoomAsync(string roomId, string userId)
        {
            var result = await _inner.EndRoomAsync(roomId, userId);
            
            if (result)
            {
                await InvalidateRoomCacheAsync(roomId, userId);
                
                // Also invalidate virtual browser cache for this room
                await _cache.RemoveAsync(CacheKeys.Generate(CacheKeys.VirtualBrowserByRoom, roomId));
            }
            
            return result;
        }

        public async Task<bool> UpdatePlaybackStateAsync(string roomId, string userId, double position, bool isPlaying)
        {
            // Playback state changes frequently - don't cache, just pass through
            // But invalidate room cache since state changed
            var result = await _inner.UpdatePlaybackStateAsync(roomId, userId, position, isPlaying);
            
            // Don't invalidate cache for playback updates - too frequent
            // The 30-second TTL will handle staleness
            
            return result;
        }

        public async Task<bool> UpdateSyncModeAsync(string roomId, string syncMode)
        {
            var result = await _inner.UpdateSyncModeAsync(roomId, syncMode);
            
            if (result)
            {
                await _cache.RemoveAsync(CacheKeys.Generate(CacheKeys.RoomById, roomId));
            }
            
            return result;
        }

        #endregion

        #region Non-cached Operations (pass through)

        public Task<bool> ValidateRoomPasswordAsync(string roomId, string? password)
        {
            // Password validation should not be cached for security
            return _inner.ValidateRoomPasswordAsync(roomId, password);
        }

        public Task<string?> GenerateInviteLink(string roomId)
        {
            // Invite link generation is cheap, no need to cache
            return _inner.GenerateInviteLink(roomId);
        }

        #endregion

        #region Helper Methods

        private async Task InvalidateRoomCacheAsync(string roomId, string? userId = null)
        {
            // Invalidate room by ID
            await _cache.RemoveAsync(CacheKeys.Generate(CacheKeys.RoomById, roomId));
            
            // Invalidate active rooms
            await _cache.RemoveAsync(CacheKeys.ActiveRooms);
            await _cache.RemoveByPatternAsync("rooms:active:page:*");
            
            // Invalidate permission checks for this room
            await _cache.RemoveByPatternAsync($"room:admin:{roomId.ToLowerInvariant()}:*");
            await _cache.RemoveByPatternAsync($"room:control:{roomId.ToLowerInvariant()}:*");
            
            // Invalidate user's rooms if userId provided
            if (!string.IsNullOrEmpty(userId))
            {
                await _cache.RemoveAsync(CacheKeys.Generate(CacheKeys.UserRooms, userId));
                await _cache.RemoveByPatternAsync($"rooms:user:{userId.ToLowerInvariant()}:page:*");
            }
            
            _logger.LogDebug("Invalidated caches for room {RoomId}", roomId);
        }

        #endregion

        /// <summary>
        /// Helper class to wrap boolean values for caching (due to class constraint on GetOrSetAsync).
        /// </summary>
        private class CachedBool
        {
            public bool Value { get; set; }
        }
    }
}
