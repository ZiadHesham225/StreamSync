using StreamSync.DTOs;

namespace StreamSync.BusinessLogic.Interfaces
{
    public interface IYouTubeService
    {
        Task<YouTubeSearchResponse> SearchVideosAsync(string query, int maxResults = 10, string? pageToken = null);
        Task<YouTubeVideoDto?> GetVideoDetailsAsync(string videoId);
        Task<string?> GetDirectVideoUrlAsync(string youtubeUrl);
        string? ExtractVideoIdFromUrl(string url);
        bool IsYouTubeUrl(string url);
    }
}
