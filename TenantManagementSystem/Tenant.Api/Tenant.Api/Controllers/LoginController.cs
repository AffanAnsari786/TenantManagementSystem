using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Tenant.Api.Data;
using Tenant.Api.Model;
using BCrypt.Net;

namespace Tenant.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoginController> _logger;

        public LoginController(AppDbContext context, IConfiguration configuration, ILogger<LoginController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
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

                // Verification with BCrypt migration
                bool isPasswordValid = false;
                if (user.Password == request.Password)
                {
                    // Plaintext match -> Migrate to BCrypt
                    user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                    isPasswordValid = true;
                }
                else
                {
                    try {
                        isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);
                    } catch { isPasswordValid = false; }
                }

                if (!isPasswordValid)
                {
                    return Unauthorized(new { message = "Invalid credentials." });
                }

                // Generate JWT
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Secret not found"));
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, user.Role ?? "tenant")
                    }),
                    Expires = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["Jwt:ExpireDays"] ?? "30")),
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"],
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var jwtToken = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(jwtToken);

                return Ok(new LoginResponse
                {
                    Token = tokenString,
                    Role = user.Role ?? "tenant",
                    Username = user.Username,
                    UserId = user.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for username {Username}", request?.Username);
                return Problem(
                    title: "Sign-in failed.",
                    detail: "An unexpected error occurred while signing in. Please try again later.",
                    statusCode: StatusCodes.Status500InternalServerError);
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
