using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.Controllers;
using StreamSync.DTOs;
using System.Security.Claims;

namespace StreamSync.Tests.Controllers
{
    public class YouTubeControllerTests
    {
        private readonly Mock<IYouTubeService> _mockYouTubeService;
        private readonly Mock<ILogger<YouTubeController>> _mockLogger;
        private readonly YouTubeController _controller;

        public YouTubeControllerTests()
        {
            _mockYouTubeService = new Mock<IYouTubeService>();
            _mockLogger = new Mock<ILogger<YouTubeController>>();

            _controller = new YouTubeController(
                _mockYouTubeService.Object,
                _mockLogger.Object);

            SetupUserContext("user-123", "testuser");
        }

        private void SetupUserContext(string userId, string username)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, username)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region SearchVideos Tests

        [Fact]
        public async Task SearchVideos_WithValidQuery_ShouldReturnResults()
        {
            // Arrange
            var query = "test video";
            var expectedResponse = new YouTubeSearchResponse
            {
                Videos = new List<YouTubeVideoDto>
                {
                    new YouTubeVideoDto
                    {
                        VideoId = "abc123",
                        Title = "Test Video",
                        Description = "A test video",
                        ChannelTitle = "Test Channel"
                    }
                },
                TotalResults = 1
            };

            _mockYouTubeService.Setup(s => s.SearchVideosAsync(query, 10, null))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.SearchVideos(query);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<YouTubeSearchResponse>().Subject;
            response.Videos.Should().HaveCount(1);
            response.Videos.First().VideoId.Should().Be("abc123");
        }

        [Fact]
        public async Task SearchVideos_WithPagination_ShouldPassParameters()
        {
            // Arrange
            var query = "test";
            var maxResults = 25;
            var pageToken = "nextPage123";
            var expectedResponse = new YouTubeSearchResponse
            {
                Videos = new List<YouTubeVideoDto>(),
                NextPageToken = "anotherPage",
                TotalResults = 100
            };

            _mockYouTubeService.Setup(s => s.SearchVideosAsync(query, maxResults, pageToken))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.SearchVideos(query, maxResults, pageToken);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            _mockYouTubeService.Verify(s => s.SearchVideosAsync(query, maxResults, pageToken), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SearchVideos_WithEmptyQuery_ShouldReturnBadRequest(string? query)
        {
            // Act
            var result = await _controller.SearchVideos(query!);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(51)]
        [InlineData(100)]
        public async Task SearchVideos_WithInvalidMaxResults_ShouldReturnBadRequest(int maxResults)
        {
            // Act
            var result = await _controller.SearchVideos("test", maxResults);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task SearchVideos_WhenServiceThrows_ShouldReturn500()
        {
            // Arrange
            _mockYouTubeService.Setup(s => s.SearchVideosAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
                .ThrowsAsync(new Exception("API error"));

            // Act
            var result = await _controller.SearchVideos("test");

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region GetVideoDetails Tests

        [Fact]
        public async Task GetVideoDetails_WithValidVideoId_ShouldReturnVideo()
        {
            // Arrange
            var videoId = "abc123";
            var expectedVideo = new YouTubeVideoDto
            {
                VideoId = videoId,
                Title = "Test Video",
                Description = "A test video",
                ChannelTitle = "Test Channel",
                Duration = TimeSpan.FromMinutes(5)
            };

            _mockYouTubeService.Setup(s => s.GetVideoDetailsAsync(videoId))
                .ReturnsAsync(expectedVideo);

            // Act
            var result = await _controller.GetVideoDetails(videoId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var video = okResult.Value.Should().BeOfType<YouTubeVideoDto>().Subject;
            video.VideoId.Should().Be(videoId);
            video.Title.Should().Be("Test Video");
        }

        [Fact]
        public async Task GetVideoDetails_WithNonExistentVideo_ShouldReturnNotFound()
        {
            // Arrange
            var videoId = "nonexistent";

            _mockYouTubeService.Setup(s => s.GetVideoDetailsAsync(videoId))
                .ReturnsAsync((YouTubeVideoDto?)null);

            // Act
            var result = await _controller.GetVideoDetails(videoId);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetVideoDetails_WithEmptyVideoId_ShouldReturnBadRequest(string? videoId)
        {
            // Act
            var result = await _controller.GetVideoDetails(videoId!);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetVideoDetails_WhenServiceThrows_ShouldReturn500()
        {
            // Arrange
            _mockYouTubeService.Setup(s => s.GetVideoDetailsAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("API error"));

            // Act
            var result = await _controller.GetVideoDetails("abc123");

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region StreamVideo Tests

        [Fact]
        public void StreamVideo_WithValidVideoId_ShouldRedirectToEmbed()
        {
            // Arrange
            var videoId = "abc123";

            // Act
            var result = _controller.StreamVideo(videoId);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
            redirectResult.Url.Should().Contain(videoId);
            redirectResult.Url.Should().Contain("youtube.com/embed");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void StreamVideo_WithEmptyVideoId_ShouldReturnBadRequest(string? videoId)
        {
            // Act
            var result = _controller.StreamVideo(videoId!);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion
    }
}
