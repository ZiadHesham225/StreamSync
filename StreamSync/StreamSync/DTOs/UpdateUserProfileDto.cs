using System.ComponentModel.DataAnnotations;

namespace StreamSync.DTOs
{
    public class UpdateUserProfileDto
    {
        [Required(ErrorMessage = "Display name is required")]
        [StringLength(50, ErrorMessage = "Display name cannot exceed 50 characters")]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Avatar URL cannot exceed 200 characters")]
        public string? AvatarUrl { get; set; }
    }
}