using Microsoft.AspNetCore.Identity;

namespace StreamSync.Models
{
    public class ApplicationUser : IdentityUser
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        // Navigation properties - keep only what's needed
        public List<Room> CreatedRooms { get; set; } = new();
        
        // Removed JoinedRooms and WatchLaterItems since participants and watch later are now in-memory/removed
    }
}
