using System.ComponentModel.DataAnnotations;

namespace StreamSync.DTOs
{
    public class ForgotPasswordRequestDto
    {
        [EmailAddress]
        public required string Email { get; set; }
    }
}
