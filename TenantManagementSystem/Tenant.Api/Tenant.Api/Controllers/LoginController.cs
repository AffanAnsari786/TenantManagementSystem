using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Tenant.Api.Common;
using Tenant.Api.Services;

namespace Tenant.Api.Controllers
{
    /// <summary>
    /// Authentication endpoints. Route is kept at /api/login to preserve the
    /// existing client contract. Access tokens are short-lived (15 min) and
    /// returned in the response body; refresh tokens are long-lived and
    /// delivered to the browser as an HttpOnly, Secure, SameSite cookie so
    /// JavaScript (and any XSS) can never read them.
    /// </summary>
    [ApiVersion("1.0")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private const string RefreshCookieName = "tms_rt";

        private readonly IAuthService _authService;
        private readonly ILogger<LoginController> _logger;
        private readonly IWebHostEnvironment _env;

        public LoginController(IAuthService authService, ILogger<LoginController> logger, IWebHostEnvironment env)
        {
            _authService = authService;
            _logger = logger;
            _env = env;
        }

        [HttpPost]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Username) || request.Password == null)
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            var tokens = await _authService.LoginAsync(request.Username, request.Password);
            if (tokens == null)
            {
                return Unauthorized(new { message = "Invalid credentials." });
            }

            AppendRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
            return Ok(new LoginResponse
            {
                Token = tokens.AccessToken,
                ExpiresAt = tokens.AccessTokenExpiresAt,
                Role = tokens.Role,
                Username = tokens.Username,
                UserId = tokens.UserId
            });
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<LoginResponse>> Refresh()
        {
            if (!Request.Cookies.TryGetValue(RefreshCookieName, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return Unauthorized(new { message = "Session expired." });
            }

            var tokens = await _authService.RefreshAsync(raw);
            if (tokens == null)
            {
                // Clear the stale cookie so the client stops retrying.
                ClearRefreshCookie();
                return Unauthorized(new { message = "Session expired." });
            }

            AppendRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
            return Ok(new LoginResponse
            {
                Token = tokens.AccessToken,
                ExpiresAt = tokens.AccessTokenExpiresAt,
                Role = tokens.Role,
                Username = tokens.Username,
                UserId = tokens.UserId
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.TryGetValue(RefreshCookieName, out var raw))
            {
                await _authService.LogoutAsync(raw);
            }
            ClearRefreshCookie();
            return Ok(new { message = "Logged out." });
        }

        private void AppendRefreshCookie(string rawRefreshToken, DateTime expiresAt)
        {
            Response.Cookies.Append(RefreshCookieName, rawRefreshToken, new CookieOptions
            {
                HttpOnly = true,
                // In dev we serve over HTTP from localhost — CORS AllowCredentials
                // requires Secure to be set per spec when SameSite=None, but we
                // use SameSite=Lax in dev (same-site-ish with localhost ports).
                Secure = !_env.IsDevelopment(),
                SameSite = _env.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.Strict,
                Expires = expiresAt,
                Path = "/api/login"
            });
        }

        private void ClearRefreshCookie()
        {
            Response.Cookies.Delete(RefreshCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = !_env.IsDevelopment(),
                SameSite = _env.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.Strict,
                Path = "/api/login"
            });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int UserId { get; set; }
    }
}
