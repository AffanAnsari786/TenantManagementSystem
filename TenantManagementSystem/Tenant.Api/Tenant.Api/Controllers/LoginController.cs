using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tenant.Api.Data;
using Tenant.Api.Model;

namespace Tenant.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LoginController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// POST api/login - validates username and password against Users table.
        /// PasswordHash column is compared as plain text (for dev); use proper hashing in production.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Username) || request.Password == null)
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username.Trim());

                if (user == null)
                {
                    return Unauthorized(new { message = "Invalid credentials." });
                }

                // Compare plain text (DB currently stores plain text in PasswordHash column)
                if (user.Password != request.Password)
                {
                    return Unauthorized(new { message = "Invalid credentials." });
                }

                var token = Guid.NewGuid().ToString("N");
                user.Token = token;
                user.TokenExpiry = DateTime.UtcNow.AddHours(24);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Ok(new LoginResponse
                {
                    Token = token,
                    Role = user.Role ?? "tenant",
                    Username = user.Username,
                    UserId = user.Id
                });
            }
            catch (Exception ex)
            {
                // Return JSON so the client never sees raw exception text; log the real error
                return StatusCode(500, new
                {
                    message = "A server error occurred while signing in. Check that the database is running and the connection string is correct.",
                    detail = ex.Message
                });
            }
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
        public string Role { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int UserId { get; set; }
    }
}
