using Microsoft.AspNetCore.SignalR;

namespace Tenant.Api.Hubs;

public class SharedDashboardHub : Hub
{
    public const string EntryUpdatedMethod = "EntryUpdated";

    public async Task JoinEntry(Guid entryId)
    {
        var groupName = $"Entry_{entryId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
}
