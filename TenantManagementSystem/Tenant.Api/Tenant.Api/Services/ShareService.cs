using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Tenant.Api.Contracts;
using Tenant.Api.Data;
using Tenant.Api.Models;

namespace Tenant.Api.Services;

public sealed class ShareService : IShareService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    // Short TTL — the shared view polls every 5s when SignalR is down, so a
    // 30-second window already removes the vast majority of DB round-trips
    // while keeping perceived staleness tiny. Mutations (record writes)
    // invalidate the cache via <see cref="InvalidateEntryPublicId"/>.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public ShareService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    internal static string ShareTokenKey(string token) => $"share:token:{token}";
    internal static string EntryPublicIdKey(Guid entryPublicId) => $"share:entry:{entryPublicId}";

    public async Task<EntryDto?> GetSharedDashboardAsync(string token)
    {
        var tokenTrimmed = (token ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(tokenTrimmed)) return null;

        var cacheKey = ShareTokenKey(tokenTrimmed);
        if (_cache.TryGetValue<EntryDto>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var dto = await LoadSharedDashboardFromDbAsync(tokenTrimmed);
        if (dto is null) return null;

        // Write to both cache keys so mutations on the entry (which only
        // know the entry PublicId, not the share token) can invalidate us.
        _cache.Set(cacheKey, dto, CacheTtl);

        var entryKey = EntryPublicIdKey(dto.Id);
        var tokensForEntry = _cache.GetOrCreate<HashSet<string>>(entryKey, e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            return new HashSet<string>();
        })!;
        tokensForEntry.Add(tokenTrimmed);

        return dto;
    }

    private async Task<EntryDto?> LoadSharedDashboardFromDbAsync(string tokenTrimmed)
    {
        var sharedLink = await _context.SharedLinks
            .FirstOrDefaultAsync(sl =>
                sl.ShareToken == tokenTrimmed &&
                sl.IsActive &&
                sl.ExpiryDate > DateTime.UtcNow);

        if (sharedLink == null) return null;

        var entry = await _context.Entries
            .Include(e => e.Records)
            .FirstOrDefaultAsync(e => e.Id == sharedLink.EntryId);

        if (entry == null) return null;

        return new EntryDto
        {
            Id = entry.PublicId,
            Name = entry.Name,
            StartDate = entry.StartDate,
            EndDate = entry.EndDate,
            // Shared (public) view intentionally omits PII like Address /
            // AadhaarNumber / PropertyName — only what the shared dashboard needs.
            Records = entry.Records?.Select(r => new RecordDto
            {
                Id = r.PublicId,
                EntryId = entry.PublicId,
                RentPeriod = r.RentPeriod,
                Amount = r.Amount,
                ReceivedDate = r.ReceivedDate,
                CreatedDate = r.CreatedDate,
                TenantSign = r.TenantSign
            }).ToList() ?? new List<RecordDto>()
        };
    }

    /// <summary>
    /// Called by write paths (record CRUD) to drop cached shared views for
    /// an entry the moment its data changes.
    /// </summary>
    public void InvalidateEntry(Guid entryPublicId)
    {
        var entryKey = EntryPublicIdKey(entryPublicId);
        if (_cache.TryGetValue<HashSet<string>>(entryKey, out var tokens) && tokens is not null)
        {
            foreach (var t in tokens)
            {
                _cache.Remove(ShareTokenKey(t));
            }
            _cache.Remove(entryKey);
        }
    }

    public async Task<ShareLinkResponse?> GenerateShareLinkAsync(int ownerUserId, ShareLinkRequest request, string shareUrlBase)
    {
        var entry = await _context.Entries
            .FirstOrDefaultAsync(e => e.PublicId == request.EntryId && e.UserId == ownerUserId);
        if (entry == null) return null;

        var token = GenerateSecureToken();
        var expiryDate = DateTime.UtcNow.AddDays(request.ExpiryDays);

        var sharedLink = new SharedLink
        {
            ShareToken = token,
            EntryId = entry.Id, // internal int id; never exposed
            CreatedDate = DateTime.UtcNow,
            ExpiryDate = expiryDate,
            IsActive = true
        };

        _context.SharedLinks.Add(sharedLink);
        await _context.SaveChangesAsync();

        var trimmedBase = (shareUrlBase ?? string.Empty).TrimEnd('/');
        return new ShareLinkResponse
        {
            ShareToken = token,
            ShareUrl = $"{trimmedBase}/shared/{token}",
            ExpiryDate = expiryDate
        };
    }

    public async Task<bool> RevokeShareLinkAsync(int ownerUserId, string token)
    {
        var sharedLink = await _context.SharedLinks.FirstOrDefaultAsync(sl => sl.ShareToken == token);
        if (sharedLink == null) return false;

        var entry = await _context.Entries
            .FirstOrDefaultAsync(e => e.Id == sharedLink.EntryId && e.UserId == ownerUserId);
        if (entry == null) return false;

        sharedLink.IsActive = false;
        await _context.SaveChangesAsync();

        // Drop cache so the revocation takes effect immediately instead of
        // waiting for the 30-second TTL.
        _cache.Remove(ShareTokenKey(token));
        InvalidateEntry(entry.PublicId);
        return true;
    }

    public async Task<IReadOnlyList<SharedLinkDto>?> GetShareLinksForEntryAsync(int ownerUserId, Guid entryPublicId)
    {
        var entry = await _context.Entries
            .FirstOrDefaultAsync(e => e.PublicId == entryPublicId && e.UserId == ownerUserId);
        if (entry == null) return null;

        var nowUtc = DateTime.UtcNow;
        var links = await _context.SharedLinks
            .Where(sl => sl.EntryId == entry.Id && sl.IsActive && sl.ExpiryDate > nowUtc)
            .Select(sl => new SharedLinkDto
            {
                ShareToken = sl.ShareToken,
                CreatedDate = sl.CreatedDate,
                ExpiryDate = sl.ExpiryDate,
                IsActive = sl.IsActive
            })
            .ToListAsync();
        return links;
    }

    private static string GenerateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }
}
