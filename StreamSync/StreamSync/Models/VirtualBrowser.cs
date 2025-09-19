namespace StreamSync.Models
{
    public class VirtualBrowser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? RoomId { get; set; } // Nullable for available browsers
        public required string ContainerId { get; set; }
        public required string ContainerName { get; set; }
        public required string BrowserUrl { get; set; }
        public required string WebRtcUrl { get; set; } // Neko WebRTC streaming URL
        public int ContainerIndex { get; set; } // 0-24 (25 containers max)
        public int HttpPort { get; set; } // HTTP port for Neko web interface
        public int UdpPortStart { get; set; } // Starting UDP port for WebRTC
        public int UdpPortEnd { get; set; } // Ending UDP port for WebRTC
        public VirtualBrowserStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AllocatedAt { get; set; }
        public DateTime? DeallocatedAt { get; set; }
        public DateTime ExpiresAt { get; set; } // 3-4 hours from allocation
        public string? BrowserState { get; set; } // JSON serialized browser state
        public string? LastAccessedUrl { get; set; }
        public string? NekoPassword { get; set; } // Neko session password
        public string? NekoAdminPassword { get; set; } // Neko admin password
        
        // Navigation properties
        public Room? Room { get; set; }
    }

    public enum VirtualBrowserStatus
    {
        Available = 0,
        Allocated = 1,
        InUse = 2,
        Deallocated = 3,
        Expired = 4,
        Error = 5
    }
}
