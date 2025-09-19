namespace StreamSync.DTOs
{
    public class YouTubeVideoDto
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string ChannelTitle { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public DateTime PublishedAt { get; set; }
        public string VideoUrl => $"https://www.youtube.com/watch?v={VideoId}";
    }

    public class YouTubeSearchResponse
    {
        public List<YouTubeVideoDto> Videos { get; set; } = new();
        public string? NextPageToken { get; set; }
        public int TotalResults { get; set; }
    }
}