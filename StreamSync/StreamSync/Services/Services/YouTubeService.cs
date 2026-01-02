using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System.Text.RegularExpressions;
using StreamSync.Services.Interfaces;
using StreamSync.DTOs;
using System.Xml;
using System.Text.Json;

namespace StreamSync.Services
{
    public class YouTubeService : IYouTubeService
    {
        private readonly Google.Apis.YouTube.v3.YouTubeService _googleYouTubeService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<YouTubeService> _logger;
        private readonly HttpClient _httpClient;

        public YouTubeService(IConfiguration configuration, ILogger<YouTubeService> logger, HttpClient httpClient)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;

            var apiKey = configuration["YouTube:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("YouTube API key is not configured");
            }

            _googleYouTubeService = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = apiKey,
                ApplicationName = "StreamSync"
            });
        }

        public async Task<YouTubeSearchResponse> SearchVideosAsync(string query, int maxResults = 10, string? pageToken = null)
        {
            try
            {
                var searchListRequest = _googleYouTubeService.Search.List("snippet");
                searchListRequest.Q = query;
                searchListRequest.MaxResults = maxResults;
                searchListRequest.Type = "video";
                searchListRequest.VideoEmbeddable = SearchResource.ListRequest.VideoEmbeddableEnum.True__;
                searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Relevance;

                if (!string.IsNullOrEmpty(pageToken))
                {
                    searchListRequest.PageToken = pageToken;
                }

                var searchListResponse = await searchListRequest.ExecuteAsync();

                var videos = new List<YouTubeVideoDto>();
                foreach (var searchResult in searchListResponse.Items)
                {
                    if (searchResult.Id.Kind == "youtube#video")
                    {
                        var video = new YouTubeVideoDto
                        {
                            VideoId = searchResult.Id.VideoId,
                            Title = searchResult.Snippet.Title,
                            Description = searchResult.Snippet.Description,
                            ThumbnailUrl = searchResult.Snippet.Thumbnails.Medium?.Url ?? searchResult.Snippet.Thumbnails.Default__.Url,
                            ChannelTitle = searchResult.Snippet.ChannelTitle,
                            PublishedAt = searchResult.Snippet.PublishedAtDateTimeOffset?.DateTime ?? DateTime.UtcNow
                        };

                        videos.Add(video);
                    }
                }

                if (videos.Any())
                {
                    await PopulateVideoDurations(videos);
                }

                return new YouTubeSearchResponse
                {
                    Videos = videos,
                    NextPageToken = searchListResponse.NextPageToken,
                    TotalResults = (int)(searchListResponse.PageInfo.TotalResults ?? 0)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching YouTube videos");
                throw;
            }
        }

        public async Task<YouTubeVideoDto?> GetVideoDetailsAsync(string videoId)
        {
            try
            {
                var videosListRequest = _googleYouTubeService.Videos.List("snippet,contentDetails,statistics");
                videosListRequest.Id = videoId;

                var videosListResponse = await videosListRequest.ExecuteAsync();
                var video = videosListResponse.Items.FirstOrDefault();

                if (video == null)
                {
                    return null;
                }

                return new YouTubeVideoDto
                {
                    VideoId = video.Id,
                    Title = video.Snippet.Title,
                    Description = video.Snippet.Description,
                    ThumbnailUrl = video.Snippet.Thumbnails.Medium?.Url ?? video.Snippet.Thumbnails.Default__.Url,
                    ChannelTitle = video.Snippet.ChannelTitle,
                    Duration = ParseYouTubeDuration(video.ContentDetails.Duration),
                    PublishedAt = video.Snippet.PublishedAtDateTimeOffset?.DateTime ?? DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting YouTube video details");
                throw;
            }
        }

        public string? ExtractVideoIdFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var patterns = new[]
            {
                @"(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/embed\/)([a-zA-Z0-9_-]{11})",
                @"youtube\.com\/watch\?.*v=([a-zA-Z0-9_-]{11})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(url, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        public bool IsYouTubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Regex.IsMatch(url, @"(youtube\.com|youtu\.be)", RegexOptions.IgnoreCase);
        }

        public Task<string?> GetDirectVideoUrlAsync(string youtubeUrl)
        {
            try
            {
                var videoId = ExtractVideoIdFromUrl(youtubeUrl);
                if (string.IsNullOrEmpty(videoId))
                    return Task.FromResult<string?>(null);
                return Task.FromResult<string?>($"/api/youtube/stream/{videoId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting direct video URL for YouTube video");
                return Task.FromResult<string?>(null);
            }
        }

        private async Task PopulateVideoDurations(List<YouTubeVideoDto> videos)
        {
            try
            {
                var videoIds = videos.Select(v => v.VideoId).ToList();
                var videosListRequest = _googleYouTubeService.Videos.List("contentDetails");
                videosListRequest.Id = string.Join(",", videoIds);

                var videosListResponse = await videosListRequest.ExecuteAsync();

                // Use Dictionary for O(1) lookup instead of O(n) FirstOrDefault
                var videoLookup = videos.ToDictionary(v => v.VideoId, v => v);
                
                foreach (var video in videosListResponse.Items)
                {
                    if (videoLookup.TryGetValue(video.Id, out var matchingDto))
                    {
                        matchingDto.Duration = ParseYouTubeDuration(video.ContentDetails.Duration);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting video durations");
            }
        }

        private static TimeSpan ParseYouTubeDuration(string duration)
        {
            try
            {
                return XmlConvert.ToTimeSpan(duration);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _googleYouTubeService?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
