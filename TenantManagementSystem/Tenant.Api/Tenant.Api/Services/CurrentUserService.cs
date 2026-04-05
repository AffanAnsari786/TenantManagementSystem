using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tenant.Api.Data;
using Tenant.Api.Model;

namespace Tenant.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<int?> GetCurrentUserIdAsync()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            return Task.FromResult<int?>(userId);
        }
        return Task.FromResult<int?>(null);
    }
}
