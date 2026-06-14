using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebHomestay.Controllers;
using WebHomestay.Data;
using WebHomestay.Hubs;
using WebHomestay.Models;
using WebHomestay.Services;
using WebHomestay.Tests;
using Xunit;

namespace WebHomestay.Tests.Controllers;

public class AdminChatMonitorControllerTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (Mock<IHubContext<ChatHub>> Hub, Mock<IHubClients> Clients, Mock<IClientProxy> AdminMonitorProxy, Mock<IClientProxy> UserProxy) CreateHubMocks()
    {
        var hub = new Mock<IHubContext<ChatHub>>();
        var clients = new Mock<IHubClients>();
        var adminMonitorProxy = new Mock<IClientProxy>();
        var userProxy = new Mock<IClientProxy>();

        clients.Setup(x => x.Group("admin_monitor")).Returns(adminMonitorProxy.Object);
        clients.Setup(x => x.Group(It.Is<string>(name => name.StartsWith("user_", StringComparison.Ordinal)))).Returns(userProxy.Object);
        hub.SetupGet(x => x.Clients).Returns(clients.Object);

        return (hub, clients, adminMonitorProxy, userProxy);
    }

    private static AdminChatMonitorController BuildController(
        ApplicationDbContext? context = null,
        IAdminChatService? adminChatService = null,
        Mock<IHubContext<ChatHub>>? hubContext = null,
        IAdminChatQuickSendService? quickSendService = null)
    {
        context ??= CreateContext();
        adminChatService ??= new AdminChatService(context);
        hubContext ??= CreateHubMocks().Hub;
        quickSendService ??= new AdminChatQuickSendService(context, new AvailabilityService(context), Mock.Of<ISettingService>());

        var controller = new AdminChatMonitorController(
            adminChatService,
            context,
            hubContext.Object,
            Mock.Of<IBookingCancellationService>(),
            Mock.Of<ISettingService>(),
            quickSendService);

        var httpContext = new DefaultHttpContext
        {
            Session = new FakeSession()
        };
        httpContext.Session.SetString("AdminUser", "admin01");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    [Fact]
    public async Task GetSessions_WithDeletedScope_ReturnsDeletedSessionsOnly()
    {
        using var context = CreateContext();
        context.AdminChatSessions.AddRange(
            new AdminChatSession
            {
                SessionId = "live-1",
                CustomerName = "Live User",
                Status = "auto",
                LastActivityAt = DateTime.Now,
                CreatedAt = DateTime.Now
            },
            new AdminChatSession
            {
                SessionId = "trash-1",
                CustomerName = "Deleted User",
                Status = "paused",
                IsDeleted = true,
                DeletedAt = DateTime.Now,
                DeletedBy = "admin01",
                LastActivityAt = DateTime.Now,
                CreatedAt = DateTime.Now
            });
        await context.SaveChangesAsync();

        var controller = BuildController(context);

        var result = await controller.GetSessions("deleted");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("trash-1", json);
        Assert.DoesNotContain("live-1", json);
    }

    [Fact]
    public async Task SendReply_WhenSessionNotTakenOver_ReturnsBadRequest()
    {
        using var context = CreateContext();
        context.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "sess-1",
            CustomerName = "Test",
            Status = "auto",
            LastActivityAt = DateTime.Now,
            CreatedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = new Mock<IAdminChatService>();
        service.Setup(x => x.IsPausedAsync("sess-1")).ReturnsAsync(false);
        var controller = BuildController(context, service.Object);

        var result = await controller.SendReply("sess-1", new AdminChatReplyRequest { Content = "xin chào" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("takeover", System.Text.Json.JsonSerializer.Serialize(badRequest.Value), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Takeover_PausesSession_AndBroadcastsAdminMonitorUpdate()
    {
        using var context = CreateContext();
        context.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "sess-2",
            CustomerName = "Test",
            Status = "auto",
            LastActivityAt = DateTime.Now,
            CreatedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        var (hub, _, adminProxy, _) = CreateHubMocks();
        var service = new AdminChatService(context);
        var controller = BuildController(context, service, hub);

        var result = await controller.Takeover("sess-2");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var session = await context.AdminChatSessions.FirstAsync(x => x.SessionId == "sess-2");
        Assert.Equal("paused", session.Status);
        Assert.Equal("manual_handoff", session.PauseReason);
        adminProxy.Verify(
            x => x.SendCoreAsync(
                "sessionUpdate",
                It.IsAny<object?[]>(),
                default),
            Times.Once);
    }

    [Fact]
    public async Task GetQuickSendSchema_ForRoomSelector_ReturnsRequiredFields()
    {
        using var context = CreateContext();
        context.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "sess-3",
            CustomerName = "Test",
            Status = "paused",
            PauseReason = "manual_handoff",
            LastActivityAt = DateTime.Now,
            CreatedAt = DateTime.Now
        });
        context.Branches.Add(new Branch
        {
            Id = 11,
            Name = "Quan 7",
            Address = "Q7",
            BookingLeadTimeHours = 2
        });
        await context.SaveChangesAsync();

        var service = new Mock<IAdminChatService>();
        service.Setup(x => x.IsPausedAsync("sess-3")).ReturnsAsync(true);
        var quickSendService = new AdminChatQuickSendService(context, new AvailabilityService(context), Mock.Of<ISettingService>());
        var controller = BuildController(context, service.Object, null, quickSendService);

        var result = await controller.GetQuickSendSchema("sess-3", "roomSelector", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("branchId", json);
        Assert.Contains("bookingMode", json);
        Assert.Contains("guestCount", json);
    }

    [Fact]
    public async Task SendQuickSendBlock_WhenMissingRequiredField_ReturnsBadRequest()
    {
        using var context = CreateContext();
        context.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "sess-4",
            CustomerName = "Test",
            Status = "paused",
            PauseReason = "manual_handoff",
            LastActivityAt = DateTime.Now,
            CreatedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = new Mock<IAdminChatService>();
        service.Setup(x => x.IsPausedAsync("sess-4")).ReturnsAsync(true);
        var quickSendService = new AdminChatQuickSendService(context, new AvailabilityService(context), Mock.Of<ISettingService>());
        var controller = BuildController(context, service.Object, null, quickSendService);

        var payload = System.Text.Json.JsonDocument.Parse("{\"bookingMode\":\"hourly\",\"guestCount\":2}").RootElement;
        var result = await controller.SendQuickSendBlock(
            "sess-4",
            "roomSelector",
            new AdminChatQuickSendRequest { Payload = payload },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var message = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Contains("chi", message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SoftDelete_RemovesSessionFromActiveScope()
    {
        using var context = CreateContext();
        context.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "sess-del",
            CustomerName = "Delete Me",
            Status = "auto",
            LastActivityAt = DateTime.Now,
            CreatedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        var (hub, _, adminProxy, _) = CreateHubMocks();
        var controller = BuildController(context, new AdminChatService(context), hub);

        var result = await controller.SoftDelete("sess-del");

        Assert.IsType<OkObjectResult>(result);
        var session = await context.AdminChatSessions.FirstAsync(x => x.SessionId == "sess-del");
        Assert.True(session.IsDeleted);
        adminProxy.Verify(
            x => x.SendCoreAsync("sessionDeleted", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task Restore_MakesDeletedSessionActiveAgain()
    {
        using var context = CreateContext();
        context.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "sess-restore",
            CustomerName = "Restore Me",
            Status = "paused",
            IsDeleted = true,
            DeletedAt = DateTime.Now,
            DeletedBy = "admin01",
            LastActivityAt = DateTime.Now,
            CreatedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        var (hub, _, adminProxy, _) = CreateHubMocks();
        var controller = BuildController(context, new AdminChatService(context), hub);

        var result = await controller.Restore("sess-restore");

        Assert.IsType<OkObjectResult>(result);
        var session = await context.AdminChatSessions.FirstAsync(x => x.SessionId == "sess-restore");
        Assert.False(session.IsDeleted);
        adminProxy.Verify(
            x => x.SendCoreAsync("sessionRestored", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task PermanentlyDelete_RemovesSessionAndMessages()
    {
        using var context = CreateContext();
        context.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "sess-purge",
            CustomerName = "Purge Me",
            Status = "paused",
            IsDeleted = true,
            DeletedAt = DateTime.Now,
            DeletedBy = "admin01",
            LastActivityAt = DateTime.Now,
            CreatedAt = DateTime.Now
        });
        context.AdminChatMessages.Add(new AdminChatMessage
        {
            SessionId = "sess-purge",
            Role = "user",
            Content = "hello",
            CreatedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        var (hub, _, adminProxy, _) = CreateHubMocks();
        var controller = BuildController(context, new AdminChatService(context), hub);

        var result = await controller.PermanentlyDelete("sess-purge");

        Assert.IsType<OkObjectResult>(result);
        Assert.False(await context.AdminChatSessions.AnyAsync(x => x.SessionId == "sess-purge"));
        Assert.False(await context.AdminChatMessages.AnyAsync(x => x.SessionId == "sess-purge"));
        adminProxy.Verify(
            x => x.SendCoreAsync("sessionPurged", It.IsAny<object?[]>(), default),
            Times.Once);
    }
}
