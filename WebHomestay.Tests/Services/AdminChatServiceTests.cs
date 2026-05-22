using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class AdminChatServiceTests
{
    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task UpsertSession_Creates_New_Session()
    {
        var ctx = CreateContext();
        var svc = new AdminChatService(ctx);

        var session = await svc.UpsertSessionAsync("sess-1", "Nguyen Van A");

        Assert.Equal("sess-1", session.SessionId);
        Assert.Equal("Nguyen Van A", session.CustomerName);
        Assert.Equal("auto", session.Status);
    }

    [Fact]
    public async Task UpsertSession_Updates_Existing_Session()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "sess-1",
            CustomerName = "Old Name",
            Status = "paused",
            CreatedAt = DateTime.Now.AddHours(-1),
            LastActivityAt = DateTime.Now.AddHours(-1)
        });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        var session = await svc.UpsertSessionAsync("sess-1", "New Name");

        Assert.Equal("sess-1", session.SessionId);
        Assert.Equal("New Name", session.CustomerName);
        Assert.Equal("paused", session.Status); // status not reset
        Assert.True(session.LastActivityAt > DateTime.Now.AddMinutes(-1));
    }

    [Fact]
    public async Task IsPaused_Returns_False_For_New_Session()
    {
        var ctx = CreateContext();
        var svc = new AdminChatService(ctx);

        var paused = await svc.IsPausedAsync("sess-unknown");

        Assert.False(paused);
    }

    [Fact]
    public async Task PauseAsync_Sets_Status_To_Paused()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession { SessionId = "sess-1", CustomerName = "Test" });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.PauseAsync("sess-1", "admin01");

        var session = await ctx.AdminChatSessions.FirstAsync(s => s.SessionId == "sess-1");
        Assert.Equal("paused", session.Status);
        Assert.Equal("admin01", session.PausedBy);
        Assert.NotNull(session.PausedAt);
    }

    [Fact]
    public async Task ResumeAsync_Sets_Status_To_Auto()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "sess-1",
            CustomerName = "Test",
            Status = "paused",
            PausedBy = "admin01",
            PausedAt = DateTime.Now
        });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.ResumeAsync("sess-1");

        var session = await ctx.AdminChatSessions.FirstAsync(s => s.SessionId == "sess-1");
        Assert.Equal("auto", session.Status);
        Assert.Null(session.PausedBy);
        Assert.Null(session.PausedAt);
    }

    [Fact]
    public async Task AddAdminReplyAsync_Saves_Message()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession { SessionId = "sess-1", CustomerName = "Test" });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.AddAdminReplyAsync("sess-1", "Hello!", "admin01");

        var msg = await ctx.AdminChatMessages.FirstAsync();
        Assert.Equal("admin", msg.Role);
        Assert.Equal("Hello!", msg.Content);
        Assert.Equal("admin01", msg.CreatedBy);
    }

    [Fact]
    public async Task AddAdminReplyAsync_Saves_FormBlock()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession { SessionId = "sess-1", CustomerName = "Test" });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.AddAdminReplyAsync("sess-1", "Chon phong", "admin01",
            formBlockJson: "{\"type\":\"roomSelector\"}", formBlockType: "roomSelector");

        var msg = await ctx.AdminChatMessages.FirstAsync();
        Assert.Equal("roomSelector", msg.FormBlockType);
        Assert.NotNull(msg.FormBlockJson);
    }

    [Fact]
    public async Task GetActiveSessionsAsync_Returns_Only_Recent()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.AddRange(
            new AdminChatSession { SessionId = "sess-1", CustomerName = "A", LastActivityAt = DateTime.Now },
            new AdminChatSession { SessionId = "sess-2", CustomerName = "B", LastActivityAt = DateTime.Now.AddHours(-2) }
        );
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        var active = await svc.GetActiveSessionsAsync(60);

        Assert.Single(active);
        Assert.Equal("sess-1", active[0].SessionId);
    }

    [Fact]
    public async Task AddSystemAutoReplyAsync_Saves_System_Message()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession { SessionId = "sess-1", CustomerName = "Test",
            AutoReplyMessage = "Xin cho doi." });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.AddSystemAutoReplyAsync("sess-1");

        var msg = await ctx.AdminChatMessages.FirstAsync();
        Assert.Equal("system", msg.Role);
        Assert.Equal("Xin cho doi.", msg.Content);
    }
}
