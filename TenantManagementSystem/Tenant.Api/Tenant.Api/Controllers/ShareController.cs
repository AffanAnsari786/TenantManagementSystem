using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Tenant.Api.Common;
using Tenant.Api.Contracts;
using Tenant.Api.Models;
using Tenant.Api.Services;

namespace Tenant.Api.Controllers
{
    [ApiVersion("1.0")]
    [EnableRateLimiting(RateLimitPolicies.Share)]
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ShareController : ControllerBase
    {
        private readonly IShareService _shareService;
        private readonly ICurrentUserService _currentUser;
        private readonly IConfiguration _configuration;

        public ShareController(IShareService shareService, ICurrentUserService currentUser, IConfiguration configuration)
        {
            _shareService = shareService;
            _currentUser = currentUser;
            _configuration = configuration;
        }

        /// <summary>
        /// Public endpoint: no login required. Anyone with the link can view the dashboard (read-only).
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{token}")]
        public async Task<ActionResult<EntryDto>> GetSharedDashboard(string token)
        {
            var dto = await _shareService.GetSharedDashboardAsync(token);
            if (dto == null)
                return NotFound(new { message = "Shared link not found or has expired." });
            return Ok(dto);
        }

        // POST: api/share/generate
        [HttpPost("generate")]
        public async Task<ActionResult<ShareLinkResponse>> GenerateShareLink([FromBody] ShareLinkRequest request)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(new { message = "Please log in to share." });

            var shareUrlBase = _configuration["Share:PublicBaseUrl"] ?? "http://localhost:4200";
            var response = await _shareService.GenerateShareLinkAsync(userId.Value, request, shareUrlBase);
            if (response == null)
                return NotFound(new { message = "Entry not found or you do not own it." });
            return Ok(response);
        }

        // DELETE: api/share/{token}
        [HttpDelete("{token}")]
        public async Task<ActionResult> RevokeShareLink(string token)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var ok = await _shareService.RevokeShareLinkAsync(userId.Value, token);
            if (!ok) return NotFound(new { message = "Shared link not found or you do not own it." });
            return Ok(new { message = "Shared link revoked successfully" });
        }

        // GET: api/share/links/{entryId}
        [HttpGet("links/{entryId}")]
        public async Task<ActionResult<IEnumerable<SharedLinkDto>>> GetShareLinksForEntry(Guid entryId)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var links = await _shareService.GetShareLinksForEntryAsync(userId.Value, entryId);
            if (links == null) return NotFound();
            return Ok(links);
        }
    }
}
