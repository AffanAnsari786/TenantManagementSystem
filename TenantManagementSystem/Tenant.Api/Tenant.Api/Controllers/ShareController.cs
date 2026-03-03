using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Tenant.Api.Data;
using Tenant.Api.Models;
using Tenant.Api.Services;

namespace Tenant.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShareController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public ShareController(AppDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Public endpoint: no login required. Anyone with the link can view the dashboard (read-only).
        /// Used to share with people who are not part of the website.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{token}")]
        public async Task<ActionResult<Entry>> GetSharedDashboard(string token)
        {
            var tokenTrimmed = (token ?? "").Trim();
            if (string.IsNullOrEmpty(tokenTrimmed))
                return NotFound(new { message = "Invalid share link" });

            var sharedLink = await _context.SharedLinks
                .FirstOrDefaultAsync(sl =>
                    sl.ShareToken == tokenTrimmed &&
                    sl.IsActive &&
                    sl.ExpiryDate > DateTime.UtcNow);

            if (sharedLink == null)
                return NotFound(new { message = "Shared link not found or has expired" });

            var entry = await _context.Entries
                .Include(e => e.Records)
                .FirstOrDefaultAsync(e => e.Id == sharedLink.EntryId);

            if (entry == null)
                return NotFound(new { message = "Dashboard data not found" });

            return Ok(entry);
        }

        // POST: api/share/generate
        [HttpPost("generate")]
        public async Task<ActionResult<ShareLinkResponse>> GenerateShareLink([FromBody] ShareLinkRequest request)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(new { message = "Please log in to share." });

            var entry = await _context.Entries.FirstOrDefaultAsync(e => e.Id == request.EntryId && e.UserId == userId);
            if (entry == null)
                return NotFound(new { message = "Entry not found or you do not own it." });

            var token = GenerateSecureToken();
            var expiryDate = DateTime.UtcNow.AddDays(request.ExpiryDays);

            var sharedLink = new SharedLink
            {
                ShareToken = token,
                EntryId = request.EntryId,
                CreatedDate = DateTime.UtcNow,
                ExpiryDate = expiryDate,
                IsActive = true
            };

            _context.SharedLinks.Add(sharedLink);
            await _context.SaveChangesAsync();

            var shareUrl = $"http://localhost:4200/shared/{token}";
            return Ok(new ShareLinkResponse
            {
                ShareToken = token,
                ShareUrl = shareUrl,
                ExpiryDate = expiryDate
            });
        }

        // DELETE: api/share/{token}
        [HttpDelete("{token}")]
        public async Task<ActionResult> RevokeShareLink(string token)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var sharedLink = await _context.SharedLinks.FirstOrDefaultAsync(sl => sl.ShareToken == token);
            if (sharedLink == null)
                return NotFound(new { message = "Shared link not found" });

            var entry = await _context.Entries.FirstOrDefaultAsync(e => e.Id == sharedLink.EntryId && e.UserId == userId);
            if (entry == null)
                return NotFound(new { message = "Shared link not found or you do not own it." });

            sharedLink.IsActive = false;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Shared link revoked successfully" });
        }

        // GET: api/share/links/{entryId}
        [HttpGet("links/{entryId}")]
        public async Task<ActionResult<IEnumerable<SharedLink>>> GetShareLinksForEntry(int entryId)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var entry = await _context.Entries.FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId);
            if (entry == null)
                return NotFound();

            var links = await _context.SharedLinks
                .Where(sl => sl.EntryId == entryId && sl.IsActive && sl.ExpiryDate > DateTime.UtcNow)
                .ToListAsync();
            return Ok(links);
        }

        private static string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
