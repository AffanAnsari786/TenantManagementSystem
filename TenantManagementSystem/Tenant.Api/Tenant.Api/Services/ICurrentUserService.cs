namespace Tenant.Api.Services;

public interface ICurrentUserService
{
    Task<int?> GetCurrentUserIdAsync();
}
