namespace StreamSync.Services.Decorators
{
    /// <summary>
    /// Cache duration constants for consistent TTL across decorators.
    /// </summary>
    public static class CacheDurations
    {
        // Room-related durations
        public static readonly TimeSpan RoomById = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan RoomByInviteCode = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan ActiveRooms = TimeSpan.FromSeconds(15);
        public static readonly TimeSpan UserRooms = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan RoomPermissionCheck = TimeSpan.FromSeconds(10);

        // User-related durations
        public static readonly TimeSpan UserProfile = TimeSpan.FromMinutes(5);

        // YouTube-related durations (longer because external API with rate limits)
        public static readonly TimeSpan YouTubeSearch = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan YouTubeVideo = TimeSpan.FromHours(6);

        // Virtual Browser durations
        public static readonly TimeSpan VirtualBrowser = TimeSpan.FromSeconds(10);
    }
}
