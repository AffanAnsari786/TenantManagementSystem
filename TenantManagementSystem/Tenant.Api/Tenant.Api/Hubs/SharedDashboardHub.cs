using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tenant.Api.Data;

namespace Tenant.Api.Hubs;
public class SharedDashboardHub : Hub
{
    public const string EntryUpdatedMethod = "EntryUpdated";

    private readonly AppDbContext _db;

    public SharedDashboardHub(AppDbContext db)
    {
        _db = db;
    }

    public async Task JoinEntry(Guid entryId, string? shareToken = null)
    {
        var authorized = await IsAuthorizedForEntryAsync(entryId, shareToken);
        if (!authorized)
        {
            throw new HubException("Not authorized to join this entry.");
        }

        var groupName = $"Entry_{entryId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    private async Task<bool> IsAuthorizedForEntryAsync(Guid entryId, string? shareToken)
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                var owns = await _db.Entries
                    .AnyAsync(e => e.PublicId == entryId && e.UserId == userId);
                if (owns) return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(shareToken))
        {
            var token = shareToken.Trim();
            var nowUtc = DateTime.UtcNow;

            var validShare = await _db.SharedLinks
                .Where(sl => sl.ShareToken == token
                          && sl.IsActive
                          && sl.ExpiryDate > nowUtc)
                .Join(_db.Entries,
                      sl => sl.EntryId,
                      e => e.Id,
                      (sl, e) => e.PublicId)
                .AnyAsync(publicId => publicId == entryId);

            if (validShare) return true;
        }

        return false;
    }
}
