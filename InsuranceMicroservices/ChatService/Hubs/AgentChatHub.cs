using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatService.Hubs;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AgentChatHub : Hub
{
    public async Task SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 4000)
            throw new HubException("Message must contain 1 to 4000 characters.");
        var avatar = Context.User?.Claims?.FirstOrDefault(c => c.Type == "avatar")?.Value;

        await Clients.All.SendAsync("ReceiveMessage", Context.User?.Identity?.Name, avatar, message);
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Others.SendAsync("ReceiveNotification", $"{Context.User?.Identity?.Name} join chat");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Clients.Others.SendAsync("ReceiveNotification", $"{Context.User?.Identity?.Name} left chat");
    }
}