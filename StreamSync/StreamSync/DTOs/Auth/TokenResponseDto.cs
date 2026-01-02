namespace StreamSync.DTOs
{
    public class TokenResponseDto
    {
        public required string AccessToken { get; set; }
        public DateTime AccessTokenExpiration { get; set; }
        public required string RefreshToken { get; set; }
        public DateTime RefreshTokenExpiration { get; set; }
    }
}
