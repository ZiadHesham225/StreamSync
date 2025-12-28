using System.ComponentModel.DataAnnotations;

namespace StreamSync.DTOs
{
    public class UpdateSyncModeDto
    {
        [Required]
        [RegularExpression("^(strict|manual)$", ErrorMessage = "SyncMode must be either 'strict' or 'manual'")]
        public required string SyncMode { get; set; }
    }
}