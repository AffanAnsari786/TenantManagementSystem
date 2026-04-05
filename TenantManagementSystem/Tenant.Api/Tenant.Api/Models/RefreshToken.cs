namespace Tenant.Api.Models;

/// <summary>
/// Server-side record of an issued refresh token. The raw token is never
/// stored — only a SHA-256 hash — so a DB leak cannot be used to forge
/// sessions. Tokens rotate on every use: the old row is marked as revoked
/// and <see cref="ReplacedByTokenHash"/> links to the successor, giving us
/// a tamper-evident chain for theft detection.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>Base64url SHA-256 hash of the raw token value.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
}
