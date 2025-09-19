namespace StreamSync.Models
{
    public class Room
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string VideoUrl { get; set; } // Changed from MovieId to VideoUrl
        public string AdminId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string InviteCode { get; set; }

        // Privacy settings
        public bool IsPrivate { get; set; } = false;
        public string? PasswordHash { get; set; }

        // Room configuration options
        public bool AutoPlay { get; set; } = true;
        public string SyncMode { get; set; } = "strict";

        // Current playback state
        public double CurrentPosition { get; set; }
        public bool IsPlaying { get; set; }

        // Virtual browser cooldown
        public DateTime? LastVirtualBrowserReleasedAt { get; set; }

        // Navigation properties
        public ApplicationUser Admin { get; set; }
        
        // Removed Movie navigation property and related collections
        // Chat messages and participants are now handled in-memory
    }
}
