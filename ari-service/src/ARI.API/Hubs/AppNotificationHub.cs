using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ARISP.Application.Hubs;

namespace ARISP.API.Hubs
{
    [Authorize]
    public class AppNotificationHub : Hub<IAppNotificationClient>
    {
        public override async Task OnConnectedAsync()
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                         ?? Context.User?.FindFirst("sub")?.Value;
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value
                        ?? Context.User?.FindFirst("role")?.Value;

            Console.WriteLine($"[SignalR CONNECT] User ID: '{userIdStr}' | Role: '{role}'");

            if (!string.IsNullOrEmpty(userIdStr))
            {
                var userGroup = $"user_{userIdStr}";
                await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);
                Console.WriteLine($"[SignalR GROUP] Joined User Group: '{userGroup}'");
            }
            if (!string.IsNullOrEmpty(role))
            {
                var roleGroup = $"role_{role.ToLower()}";
                await Groups.AddToGroupAsync(Context.ConnectionId, roleGroup);
                Console.WriteLine($"[SignalR GROUP] Joined Role Group: '{roleGroup}'");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                         ?? Context.User?.FindFirst("sub")?.Value;
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value
                        ?? Context.User?.FindFirst("role")?.Value;

            if (!string.IsNullOrEmpty(userIdStr))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userIdStr}");
            }
            if (!string.IsNullOrEmpty(role))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"role_{role.ToLower()}");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
