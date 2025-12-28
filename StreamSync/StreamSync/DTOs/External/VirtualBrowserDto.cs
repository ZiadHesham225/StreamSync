namespace StreamSync.DTOs
{
    public class VirtualBrowserDto
    {
        public string Id { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string ContainerId { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
        public string BrowserUrl { get; set; } = string.Empty;
        public string WebRtcUrl { get; set; } = string.Empty;
        public int ContainerIndex { get; set; }
        public int HttpPort { get; set; }
        public int UdpPortStart { get; set; }
        public int UdpPortEnd { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? AllocatedAt { get; set; }
        public DateTime? DeallocatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? LastAccessedUrl { get; set; }
        public string? NekoPassword { get; set; }
        public string? NekoAdminPassword { get; set; }
        public TimeSpan? TimeRemaining { get; set; }
    }

    public class VirtualBrowserRequestDto
    {
        public required string RoomId { get; set; }
    }

    public class VirtualBrowserControlDto
    {
        public required string VirtualBrowserId { get; set; }
        public string Action { get; set; } = string.Empty; // "mouseMove", "click", "scroll", "keypress", etc.
        public object? Data { get; set; } // Action-specific data (coordinates, key, etc.)
    }

    public class VirtualBrowserNavigateDto
    {
        public required string VirtualBrowserId { get; set; }
        public required string Url { get; set; }
    }

    public class VirtualBrowserQueueDto
    {
        public string Id { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public int Position { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? NotifiedAt { get; set; }
        public DateTime? NotificationExpiresAt { get; set; }
        public TimeSpan? NotificationTimeRemaining { get; set; }
    }
}
