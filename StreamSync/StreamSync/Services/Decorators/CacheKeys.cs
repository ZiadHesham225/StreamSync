namespace StreamSync.Services.Decorators
{
    /// <summary>
    /// Cache key constants for consistent key generation across decorators.
    /// </summary>
    public static class CacheKeys
    {
        // Room cache keys
        public const string RoomById = "room:id:{0}";
        public const string RoomByInviteCode = "room:invite:{0}";
        public const string ActiveRooms = "rooms:active";
        public const string ActiveRoomsPaged = "rooms:active:page:{0}:size:{1}:sort:{2}:order:{3}:search:{4}";
        public const string UserRooms = "rooms:user:{0}";
        public const string UserRoomsPaged = "rooms:user:{0}:page:{1}:size:{2}:sort:{3}:order:{4}:search:{5}";
        public const string RoomAdminCheck = "room:admin:{0}:{1}";
        public const string RoomControlCheck = "room:control:{0}:{1}";

        // User cache keys
        public const string UserProfile = "user:profile:{0}";

        // YouTube cache keys
        public const string YouTubeSearch = "youtube:search:{0}:{1}:{2}"; // query:maxResults:pageToken
        public const string YouTubeVideo = "youtube:video:{0}";

        // Virtual Browser cache keys
        public const string VirtualBrowserByRoom = "vb:room:{0}";
        public const string VirtualBrowserById = "vb:id:{0}";

        /// <summary>
        /// Generates a cache key using the provided format and arguments.
        /// </summary>
        public static string Generate(string format, params object?[] args)
        {
            var sanitizedArgs = args.Select(a => a?.ToString()?.ToLowerInvariant() ?? "null").ToArray();
            return string.Format(format, sanitizedArgs);
        }
    }
}
