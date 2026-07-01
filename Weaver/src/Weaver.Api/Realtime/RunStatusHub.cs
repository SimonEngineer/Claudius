using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Weaver.Api.Realtime;

/// <summary>
/// Pushes "runStatusChanged" events to whichever browser tabs are connected as a given user, so
/// the scrape/workflow run tables can update live instead of polling every few seconds. Clients
/// don't subscribe to individual runs -- the group is just "this user", since a resource list page
/// wants to know about ALL of that user's runs, not one at a time.
/// </summary>
[Authorize]
public class RunStatusHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.GetUserId();
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId.Value));
        }

        await base.OnConnectedAsync();
    }

    public static string GroupName(Guid userId) => $"user:{userId}";
}
