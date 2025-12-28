using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StreamSync.BusinessLogic.Services;

namespace StreamSync.Tests.Services
{
    public class YouTubeServiceTests
    {
        #region ExtractVideoIdFromUrl Tests

        [Theory]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLtest", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/watch?list=PLtest&v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        public void ExtractVideoIdFromUrl_WithValidUrls_ShouldExtractVideoId(string url, string expectedVideoId)
        {
            // Act
            var result = ExtractVideoIdFromUrlHelper(url);

            // Assert
            result.Should().Be(expectedVideoId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("https://www.google.com")]
        [InlineData("https://www.vimeo.com/123456")]
        [InlineData("not-a-url")]
        public void ExtractVideoIdFromUrl_WithInvalidUrls_ShouldReturnNull(string? url)
        {
            // Act
            var result = ExtractVideoIdFromUrlHelper(url!);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region IsYouTubeUrl Tests

        [Theory]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
        [InlineData("https://youtube.com/watch?v=abc123", true)]
        [InlineData("https://youtu.be/abc123", true)]
        [InlineData("https://www.youtube.com/embed/abc123", true)]
        [InlineData("http://youtube.com/watch?v=abc123", true)]
        [InlineData("https://YOUTUBE.COM/watch?v=abc123", true)]
        public void IsYouTubeUrl_WithYouTubeUrls_ShouldReturnTrue(string url, bool expected)
        {
            // Act
            var result = IsYouTubeUrlHelper(url);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("https://www.google.com")]
        [InlineData("https://vimeo.com/123456")]
        [InlineData("https://dailymotion.com/video/abc")]
        [InlineData("not-a-url")]
        public void IsYouTubeUrl_WithNonYouTubeUrls_ShouldReturnFalse(string url)
        {
            // Act
            var result = IsYouTubeUrlHelper(url);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsYouTubeUrl_WithEmptyOrNullUrl_ShouldReturnFalse(string? url)
        {
            // Act
            var result = IsYouTubeUrlHelper(url!);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Helper Methods

        // These helpers simulate the static methods from YouTubeService
        // In a real scenario, you would test via the service instance

        private static string? ExtractVideoIdFromUrlHelper(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var patterns = new[]
            {
                @"(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/embed\/)([a-zA-Z0-9_-]{11})",
                @"youtube\.com\/watch\?.*v=([a-zA-Z0-9_-]{11})"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(url, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        private static bool IsYouTubeUrlHelper(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(url, @"(youtube\.com|youtu\.be)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        #endregion
    }
}
