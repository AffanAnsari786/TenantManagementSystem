namespace Tenant.Api.Services;

public interface IAuthService
{
    /// <summary>
    /// Authenticates a user and issues an access token + refresh token pair.
    /// Returns null when credentials are invalid.
    /// </summary>
    Task<AuthTokens?> LoginAsync(string username, string password);

    /// <summary>
    /// Exchanges a valid refresh token for a new access token + rotated
    /// refresh token. Returns null when the supplied token is missing,
    /// unknown, revoked, expired, or reused (which triggers a forced revoke
    /// of the whole chain for that user).
    /// </summary>
    Task<AuthTokens?> RefreshAsync(string rawRefreshToken);

    /// <summary>
    /// Revokes the supplied refresh token (sign-out). Idempotent.
    /// </summary>
    Task LogoutAsync(string? rawRefreshToken);
}

public sealed class AuthTokens
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int UserId { get; set; }
}
