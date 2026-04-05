using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Tenant.Api.Data;
using Tenant.Api.Model;
using Tenant.Api.Models;

namespace Tenant.Api.Services;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthTokens?> LoginAsync(string username, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username.Trim());
        if (user == null) return null;

        if (!VerifyPassword(user, password)) return null;

        await _context.SaveChangesAsync(); // persist any bcrypt migration from VerifyPassword
        return await IssueTokensAsync(user);
    }

    public async Task<AuthTokens?> RefreshAsync(string rawRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return null;

        var hash = HashToken(rawRefreshToken);
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored == null) return null;

        // Reuse detection: if a token that has already been rotated (revoked
        // with a ReplacedByTokenHash) is presented again, assume it was stolen
        // and revoke every outstanding token for that user.
        if (stored.RevokedAt != null)
        {
            _logger.LogWarning("Refresh token reuse detected for user {UserId}. Revoking all tokens.", stored.UserId);
            await RevokeAllForUserAsync(stored.UserId);
            await _context.SaveChangesAsync();
            return null;
        }

        if (DateTime.UtcNow >= stored.ExpiresAt)
        {
            return null;
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId);
        if (user == null) return null;

        // Rotate: revoke old, issue new, link them.
        var newTokens = await IssueTokensAsync(user);
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByTokenHash = HashToken(newTokens.RefreshToken);
        await _context.SaveChangesAsync();
        return newTokens;
    }

    public async Task LogoutAsync(string? rawRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return;

        var hash = HashToken(rawRefreshToken);
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored == null || stored.RevokedAt != null) return;

        stored.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    // -- internals ----------------------------------------------------------

    private bool VerifyPassword(User user, string password)
    {
        // Legacy plaintext migration — compares plaintext once, then rewrites
        // the column as a BCrypt hash. Any users created after this commit
        // will already be BCrypt.
        if (user.Password == password)
        {
            user.Password = BCrypt.Net.BCrypt.HashPassword(password);
            _context.Users.Update(user);
            return true;
        }
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, user.Password);
        }
        catch
        {
            return false;
        }
    }

    private async Task<AuthTokens> IssueTokensAsync(User user)
    {
        var accessMinutes = double.TryParse(_configuration["Jwt:AccessTokenMinutes"], out var m) ? m : 15;
        var refreshDays = double.TryParse(_configuration["Jwt:RefreshTokenDays"], out var d) ? d : 7;

        var accessExpiresAt = DateTime.UtcNow.AddMinutes(accessMinutes);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(refreshDays);

        var accessToken = BuildJwt(user, accessExpiresAt);
        var rawRefresh = GenerateRawRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawRefresh),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshExpiresAt
        });
        await _context.SaveChangesAsync();

        return new AuthTokens
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshToken = rawRefresh,
            RefreshTokenExpiresAt = refreshExpiresAt,
            Role = user.Role ?? "tenant",
            Username = user.Username,
            UserId = user.Id
        };
    }

    private string BuildJwt(User user, DateTime expiresAt)
    {
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role ?? "tenant")
            }),
            Expires = expiresAt,
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static string GenerateRawRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }

    private static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }

    private async Task RevokeAllForUserAsync(int userId)
    {
        var active = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var t in active) t.RevokedAt = now;
    }
}
