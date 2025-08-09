using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using Tenant.Api.Models;

namespace Tenant.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShareController : ControllerBase
    {
        // Static in-memory data for shared links
        private static List<SharedLink> SharedLinks = new List<SharedLink>();
        private static int shareLinkIdCounter = 1;

        // GET: api/share/{token}
        [HttpGet("{token}")]
        public ActionResult<Entry> GetSharedDashboard(string token)
        {
            // Find the shared link
            var sharedLink = SharedLinks.FirstOrDefault(sl =>
                sl.ShareToken == token &&
                sl.IsActive &&
                sl.ExpiryDate > DateTime.UtcNow);

            if (sharedLink == null)
            {
                return NotFound(new { message = "Shared link not found or has expired" });
            }

            // Get the entry data from EntriesController
            var entry = EntriesController.GetEntryById(sharedLink.EntryId);

            if (entry == null)
            {
                return NotFound(new { message = "Dashboard data not found" });
            }

            return Ok(entry);
        }

        // POST: api/share/generate
        [HttpPost("generate")]
        public ActionResult<ShareLinkResponse> GenerateShareLink([FromBody] ShareLinkRequest request)
        {
            // Check if entry exists
            var entry = EntriesController.GetEntryById(request.EntryId);
            if (entry == null)
            {
                return NotFound(new { message = "Entry not found" });
            }

            // Generate unique token
            var token = GenerateSecureToken();
            var expiryDate = DateTime.UtcNow.AddDays(request.ExpiryDays);

            // Create shared link
            var sharedLink = new SharedLink
            {
                Id = shareLinkIdCounter++,
                ShareToken = token,
                EntryId = request.EntryId,
                CreatedDate = DateTime.UtcNow,
                ExpiryDate = expiryDate,
                IsActive = true
            };

            SharedLinks.Add(sharedLink);

            // Generate share URL (you might want to configure the base URL)
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var shareUrl = $"http://localhost:4200/shared/{token}"; // Angular client URL using HTTP

            var response = new ShareLinkResponse
            {
                ShareToken = token,
                ShareUrl = shareUrl,
                ExpiryDate = expiryDate
            };

            return Ok(response);
        }

        // DELETE: api/share/{token}
        [HttpDelete("{token}")]
        public ActionResult RevokeShareLink(string token)
        {
            var sharedLink = SharedLinks.FirstOrDefault(sl => sl.ShareToken == token);
            if (sharedLink == null)
            {
                return NotFound(new { message = "Shared link not found" });
            }

            sharedLink.IsActive = false;
            return Ok(new { message = "Shared link revoked successfully" });
        }

        // GET: api/share/links/{entryId}
        [HttpGet("links/{entryId}")]
        public ActionResult<IEnumerable<SharedLink>> GetShareLinksForEntry(int entryId)
        {
            var links = SharedLinks
                .Where(sl => sl.EntryId == entryId && sl.IsActive && sl.ExpiryDate > DateTime.UtcNow)
                .ToList();

            return Ok(links);
        }

        private string GenerateSecureToken()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[32];
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");
            }
        }
    }
}
