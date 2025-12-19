using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace StreamSync.Controllers
{
    public abstract class BaseApiController : ControllerBase
    {
        protected string? GetAuthenticatedUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        protected IActionResult UnauthorizedUser()
        {
            return Unauthorized();
        }
    }
}
