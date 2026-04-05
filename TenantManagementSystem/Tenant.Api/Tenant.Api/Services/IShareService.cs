using Tenant.Api.Contracts;
using Tenant.Api.Models;

namespace Tenant.Api.Services;

public interface IShareService
{
    /// <summary>
    /// Public read-only fetch of an entry via its share token. Returns null
    /// when the token is invalid, revoked, or expired.
    /// </summary>
    Task<EntryDto?> GetSharedDashboardAsync(string token);

    /// <summary>
    /// Generates a new share link for an entry the caller owns. Returns null
    /// when the caller does not own the entry.
    /// </summary>
    Task<ShareLinkResponse?> GenerateShareLinkAsync(int ownerUserId, ShareLinkRequest request, string shareUrlBase);

    /// <summary>
    /// Revokes (soft-deletes) a share link the caller owns.
    /// </summary>
    Task<bool> RevokeShareLinkAsync(int ownerUserId, string token);

    /// <summary>
    /// Lists active, non-expired share links for an entry the caller owns.
    /// Returns null when the caller does not own the entry.
    /// </summary>
    Task<IReadOnlyList<SharedLinkDto>?> GetShareLinksForEntryAsync(int ownerUserId, Guid entryPublicId);

    /// <summary>
    /// Drops cached shared-view data for an entry (fires on mutations so
    /// viewers see updates sooner than the cache TTL would permit).
    /// </summary>
    void InvalidateEntry(Guid entryPublicId);
}

public sealed class SharedLinkDto
{
    public string ShareToken { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; }
}
