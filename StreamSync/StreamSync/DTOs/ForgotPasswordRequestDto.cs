using System.ComponentModel.DataAnnotations;

namespace StreamSync.DTOs
{
    public class ForgotPasswordRequestDto
    {
        [EmailAddress]
        public string Email { get; set; }
    }
}
