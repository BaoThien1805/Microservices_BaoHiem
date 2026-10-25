using System.Security.Claims;
using ChatService.Api.Commands;
using ChatService.Commands;
using ChatService.Hubs;
using ChatService.Listeners;
using Microsoft.AspNetCore.SignalR;
using Moq;
using PolicyService.Api.Events;

namespace ChatService.Test;
public class ChatTests
{
    [Fact]
    public async Task SystemNotificationUsesNotificationEvent()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.SetupGet(c => c.All).Returns(proxy.Object);
        var context = new Mock<IHubContext<AgentChatHub>>();
        context.SetupGet(c => c.Clients).Returns(clients.Object);
        await new SendNotificationHandler(context.Object).Handle(new SendNotificationCommand { Message = "Hello" }, default);
        proxy.Verify(p => p.SendCoreAsync("ReceiveNotification",
            It.Is<object?[]>(a => a.Length == 1 && (string)a[0]! == "Hello"), default), Times.Once);
    }

    [Fact]
    public async Task PolicyCreatedBroadcastsSalesNotification()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.SetupGet(c => c.All).Returns(proxy.Object);
        var context = new Mock<IHubContext<AgentChatHub>>();
        context.SetupGet(c => c.Clients).Returns(clients.Object);
        await new PolicyCreatedHandler(context.Object).Handle(new PolicyCreated { AgentLogin = "jimmy.solid", ProductCode = "TRI" }, default);
        proxy.Verify(p => p.SendCoreAsync("ReceiveNotification",
            It.Is<object?[]>(a => a.Length == 1 && ((string)a[0]!).Contains("jimmy.solid") && ((string)a[0]!).Contains("TRI")), default), Times.Once);
    }

    [Fact]
    public async Task AgentMessageCarriesNameAvatarAndText()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.All).Returns(proxy.Object);
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "jimmy.solid"), new Claim("avatar", "jimmy.png") }, "test")));
        var hub = new AgentChatHub { Clients = clients.Object, Context = context.Object };
        await hub.SendMessage("hello");
        proxy.Verify(p => p.SendCoreAsync("ReceiveMessage", It.Is<object?[]>(a =>
            a.Length == 3 && (string)a[0]! == "jimmy.solid" && (string)a[1]! == "jimmy.png" && (string)a[2]! == "hello"), default), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4001)]
    public async Task InvalidMessageIsRejected(int length) =>
        await Assert.ThrowsAsync<HubException>(() => new AgentChatHub().SendMessage(new string('a', length)));
}
