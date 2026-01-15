using StreamSync.DataAccess.Interfaces;
using StreamSync.DTOs;
using StreamSync.Services.Interfaces;

namespace StreamSync.Services.Decorators
{
    /// <summary>
    /// Caching decorator for IYouTubeService.
    /// Caches YouTube API responses to reduce API quota usage and improve performance.
    /// </summary>
    public class YouTubeServiceCachingDecorator : IYouTubeService
    {
        private readonly IYouTubeService _inner;
        private readonly ICacheService _cache;
        private readonly ILogger<YouTubeServiceCachingDecorator> _logger;

        public YouTubeServiceCachingDecorator(
            IYouTubeService inner,
            ICacheService cache,
            ILogger<YouTubeServiceCachingDecorator> logger)
        {
            _inner = inner;
            _cache = cache;
            _logger = logger;
        }

        public async Task<YouTubeSearchResponse> SearchVideosAsync(string query, int maxResults = 10, string? pageToken = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new YouTubeSearchResponse { Videos = new List<YouTubeVideoDto>() };
            }

            // Normalize the query for consistent cache keys
            var normalizedQuery = query.Trim().ToLowerInvariant();
            var cacheKey = CacheKeys.Generate(
                CacheKeys.YouTubeSearch,
                normalizedQuery,
                maxResults,
                pageToken ?? "first");

            var cached = await _cache.GetAsync<YouTubeSearchResponse>(cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("YouTube search cache hit for query: {Query}", query);
                return cached;
            }

            _logger.LogDebug("YouTube search cache miss for query: {Query}, calling API", query);
            var result = await _inner.SearchVideosAsync(query, maxResults, pageToken);
            
            // Cache successful responses
            if (result.Videos.Any())
            {
                await _cache.SetAsync(cacheKey, result, CacheDurations.YouTubeSearch);
                
                // Also cache individual video details from search results
                foreach (var video in result.Videos)
                {
                    var videoKey = CacheKeys.Generate(CacheKeys.YouTubeVideo, video.VideoId);
                    await _cache.SetAsync(videoKey, video, CacheDurations.YouTubeVideo);
                }
            }

            return result;
        }

        public async Task<YouTubeVideoDto?> GetVideoDetailsAsync(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                return null;

            var cacheKey = CacheKeys.Generate(CacheKeys.YouTubeVideo, videoId);

            return await _cache.GetOrSetAsync(
                cacheKey,
                () => _inner.GetVideoDetailsAsync(videoId),
                CacheDurations.YouTubeVideo);
        }

        // These methods don't need caching - they're either stateless utilities or return ephemeral data

        public Task<string?> GetDirectVideoUrlAsync(string youtubeUrl)
        {
            // Direct video URLs may expire, so we don't cache them
            return _inner.GetDirectVideoUrlAsync(youtubeUrl);
        }

        public string? ExtractVideoIdFromUrl(string url)
        {
            // Pure utility function, no external calls
            return _inner.ExtractVideoIdFromUrl(url);
        }

        public bool IsYouTubeUrl(string url)
        {
            // Pure utility function, no external calls
            return _inner.IsYouTubeUrl(url);
        }
    }
}
