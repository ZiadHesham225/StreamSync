using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.DTOs;

namespace StreamSync.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VirtualBrowserController : BaseApiController
    {
        private readonly IVirtualBrowserService _virtualBrowserService;
        private readonly IRoomService _roomService;
        private readonly ILogger<VirtualBrowserController> _logger;

        public VirtualBrowserController(
            IVirtualBrowserService virtualBrowserService,
            IRoomService roomService,
            ILogger<VirtualBrowserController> logger)
        {
            _virtualBrowserService = virtualBrowserService;
            _roomService = roomService;
            _logger = logger;
        }

        [Authorize]
        [HttpPost("request")]
        public async Task<IActionResult> RequestVirtualBrowser([FromBody] VirtualBrowserRequestDto request)
        {
            var userId = GetAuthenticatedUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var canControl = await _roomService.CanUserControlRoomAsync(request.RoomId, userId);
            if (!canControl)
            {
                return Forbid("Only room admins or controllers can request virtual browsers");
            }

            var result = await _virtualBrowserService.RequestVirtualBrowserAsync(request.RoomId);
            
            if (result != null)
            {
                return Ok(result);
            }

            var queueStatus = await _virtualBrowserService.GetRoomQueueStatusAsync(request.RoomId);
            if (queueStatus != null)
            {
                return Ok(new { message = "Added to queue", queue = queueStatus });
            }

            return BadRequest("Failed to request virtual browser");
        }

        [Authorize]
        [HttpPost("release/{roomId}")]
        public async Task<IActionResult> ReleaseVirtualBrowser(string roomId)
        {
            var userId = GetAuthenticatedUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var canControl = await _roomService.CanUserControlRoomAsync(roomId, userId);
            if (!canControl)
            {
                return Forbid("Only room admins or controllers can release virtual browsers");
            }

            var result = await _virtualBrowserService.ReleaseVirtualBrowserAsync(roomId);
            
            if (result)
            {
                return Ok(new { message = "Virtual browser released successfully" });
            }

            return BadRequest("Failed to release virtual browser");
        }

        [HttpGet("room/{roomId}")]
        public async Task<IActionResult> GetRoomVirtualBrowser(string roomId)
        {
            var browser = await _virtualBrowserService.GetRoomVirtualBrowserAsync(roomId);
            
            if (browser != null)
            {
                return Ok(browser);
            }

            // Check queue status
            var queueStatus = await _virtualBrowserService.GetRoomQueueStatusAsync(roomId);
            if (queueStatus != null)
            {
                return Ok(new { queue = queueStatus });
            }

            return NotFound("No virtual browser or queue entry found for this room");
        }

        [HttpGet("cooldown/{roomId}")]
        public async Task<IActionResult> GetRoomCooldownStatus(string roomId)
        {
            var cooldownInfo = await _virtualBrowserService.GetRoomCooldownStatusAsync(roomId);
            return Ok(cooldownInfo);
        }

        [Authorize]
        [HttpPost("queue/accept/{roomId}")]
        public async Task<IActionResult> AcceptQueueNotification(string roomId)
        {
            var userId = GetAuthenticatedUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var canControl = await _roomService.CanUserControlRoomAsync(roomId, userId);
            if (!canControl)
            {
                return Forbid("Only room admins or controllers can accept queue notifications");
            }

            var result = await _virtualBrowserService.AcceptQueueNotificationAsync(roomId);
            
            if (result)
            {
                return Ok(new { message = "Queue notification accepted, virtual browser allocated" });
            }

            return BadRequest("Failed to accept queue notification");
        }

        [Authorize]
        [HttpPost("queue/decline/{roomId}")]
        public async Task<IActionResult> DeclineQueueNotification(string roomId)
        {
            var userId = GetAuthenticatedUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var canControl = await _roomService.CanUserControlRoomAsync(roomId, userId);
            if (!canControl)
            {
                return Forbid("Only room admins or controllers can decline queue notifications");
            }

            var result = await _virtualBrowserService.DeclineQueueNotificationAsync(roomId);
            
            if (result)
            {
                return Ok(new { message = "Queue notification declined" });
            }

            return BadRequest("Failed to decline queue notification");
        }

        [Authorize]
        [HttpPost("queue/cancel/{roomId}")]
        public async Task<IActionResult> CancelQueue(string roomId)
        {
            var userId = GetAuthenticatedUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var canControl = await _roomService.CanUserControlRoomAsync(roomId, userId);
            if (!canControl)
            {
                return Forbid("Only room admins or controllers can cancel queue");
            }

            var result = await _virtualBrowserService.CancelQueueAsync(roomId);
            
            if (result)
            {
                return Ok(new { message = "Queue cancelled successfully" });
            }

            return BadRequest("Failed to cancel queue");
        }

        [Authorize]
        [HttpPost("navigate")]
        public async Task<IActionResult> NavigateVirtualBrowser([FromBody] VirtualBrowserNavigateDto request)
        {
            var userId = GetAuthenticatedUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var browser = await _virtualBrowserService.GetRoomVirtualBrowserAsync(request.VirtualBrowserId);
            if (browser == null)
            {
                return NotFound("Virtual browser not found");
            }

            var canControl = await _roomService.CanUserControlRoomAsync(browser.RoomId, userId);
            if (!canControl)
            {
                return Forbid("Only room admins or controllers can navigate virtual browsers");
            }

            var result = await _virtualBrowserService.NavigateVirtualBrowserAsync(request.VirtualBrowserId, request.Url);
            
            if (result)
            {
                return Ok(new { message = "Navigation successful" });
            }

            return BadRequest("Failed to navigate virtual browser");
        }

        [Authorize]
        [HttpPost("control")]
        public async Task<IActionResult> ControlVirtualBrowser([FromBody] VirtualBrowserControlDto request)
        {
            var userId = GetAuthenticatedUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var browser = await _virtualBrowserService.GetRoomVirtualBrowserAsync(request.VirtualBrowserId);
            if (browser == null)
            {
                return NotFound("Virtual browser not found");
            }

            var isAdmin = await _roomService.IsUserAdminAsync(browser.RoomId, userId);
            if (!isAdmin)
            {
                return Forbid("Only room admins can control virtual browsers");
            }

            var result = await _virtualBrowserService.ControlVirtualBrowserAsync(request.VirtualBrowserId, request);
            
            if (result)
            {
                return Ok(new { message = "Control action successful" });
            }

            return BadRequest("Failed to execute control action");
        }

        [Authorize]
        [HttpPost("restart/{virtualBrowserId}")]
        public async Task<IActionResult> RestartBrowserProcess(string virtualBrowserId)
        {
            var userId = GetAuthenticatedUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var browser = await _virtualBrowserService.GetVirtualBrowserAsync(virtualBrowserId);
            if (browser == null)
            {
                return NotFound("Virtual browser not found");
            }

            var isAdmin = await _roomService.IsUserAdminAsync(browser.RoomId, userId);
            if (!isAdmin)
            {
                return Forbid("Only room admins can restart virtual browsers");
            }

            var result = await _virtualBrowserService.RestartBrowserProcessAsync(virtualBrowserId);
            
            if (result)
            {
                return Ok(new { message = "Browser process restarted successfully" });
            }

            return BadRequest("Failed to restart browser process");
        }

        [Authorize]
        [HttpGet("admin/all")]
        public async Task<IActionResult> GetAllVirtualBrowsers()
        {
            var browsers = await _virtualBrowserService.GetAllVirtualBrowsersAsync();
            return Ok(browsers);
        }

        [Authorize]
        [HttpGet("admin/queue")]
        public async Task<IActionResult> GetQueueStatus()
        {
            var queue = await _virtualBrowserService.GetQueueStatusAsync();
            return Ok(queue);
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { status = "Virtual Browser service is running", timestamp = DateTime.UtcNow });
        }
    }
}
