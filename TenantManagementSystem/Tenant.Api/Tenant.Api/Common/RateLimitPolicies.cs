namespace Tenant.Api.Common;

/// <summary>
/// Named rate-limit policies. Keep controllers referring to these constants
/// instead of magic strings so renames stay safe.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>5 requests / minute / IP. Guards credential-stuffing on /api/login.</summary>
    public const string Login = "login";

    /// <summary>60 requests / minute / IP. Guards scrapers on /api/share/*.</summary>
    public const string Share = "share";
}
