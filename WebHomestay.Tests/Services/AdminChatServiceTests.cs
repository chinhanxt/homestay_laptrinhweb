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
            PausedAt = DateTime.Now,
            PauseReason = "manual_handoff",
            TakenOverBy = "admin01",
            TakenOverAt = DateTime.Now
        });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.ResumeAsync("sess-1");

        var session = await ctx.AdminChatSessions.FirstAsync(s => s.SessionId == "sess-1");
        Assert.Equal("auto", session.Status);
        Assert.Null(session.PausedBy);
        Assert.Null(session.PausedAt);
        Assert.Null(session.PauseReason);
        Assert.Null(session.TakenOverBy);
        Assert.Null(session.TakenOverAt);
    }

    [Fact]
    public async Task SoftDeleteSession_Marks_Session_Deleted_Without_Removing_Messages()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession { SessionId = "sess-1", CustomerName = "Test" });
        ctx.AdminChatMessages.Add(new AdminChatMessage
        {
            SessionId = "sess-1",
            Role = "user",
            Content = "Can I book?"
        });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.SoftDeleteSessionAsync("sess-1", "admin01");

        var session = await ctx.AdminChatSessions.FirstAsync(s => s.SessionId == "sess-1");
        var messages = await ctx.AdminChatMessages.Where(m => m.SessionId == "sess-1").ToListAsync();

        Assert.True(session.IsDeleted);
        Assert.Equal("admin01", session.DeletedBy);
        Assert.NotNull(session.DeletedAt);
        Assert.Single(messages);
        Assert.Equal("Can I book?", messages[0].Content);
    }

    [Fact]
    public async Task TakeoverSession_Sets_Paused_And_Manual_Handoff_Metadata()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession { SessionId = "sess-1", CustomerName = "Test" });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.TakeoverSessionAsync("sess-1", "admin01");

        var session = await ctx.AdminChatSessions.FirstAsync(s => s.SessionId == "sess-1");

        Assert.Equal("paused", session.Status);
        Assert.Equal("admin01", session.PausedBy);
        Assert.NotNull(session.PausedAt);
        Assert.Equal("manual_handoff", session.PauseReason);
        Assert.Equal("admin01", session.TakenOverBy);
        Assert.NotNull(session.TakenOverAt);
    }

    [Fact]
    public async Task UpsertSession_Revives_Deleted_Session_As_Clean_Active_State()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "sess-1",
            CustomerName = "Old Test",
            Status = "paused",
            PausedBy = "admin01",
            PausedAt = DateTime.Now.AddMinutes(-10),
            PauseReason = "manual_handoff",
            TakenOverBy = "admin01",
            TakenOverAt = DateTime.Now.AddMinutes(-10),
            IsDeleted = true,
            DeletedAt = DateTime.Now.AddMinutes(-5),
            DeletedBy = "admin02"
        });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        var session = await svc.UpsertSessionAsync("sess-1", "Revived Name");

        Assert.False(session.IsDeleted);
        Assert.Equal("auto", session.Status);
        Assert.Null(session.PausedBy);
        Assert.Null(session.PausedAt);
        Assert.Null(session.PauseReason);
        Assert.Null(session.TakenOverBy);
        Assert.Null(session.TakenOverAt);
        Assert.Null(session.DeletedAt);
        Assert.Null(session.DeletedBy);
        Assert.Equal("Revived Name", session.CustomerName);
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
    public async Task GetSessionsAsync_Separates_Active_And_Deleted_Scopes()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.AddRange(
            new AdminChatSession
            {
                SessionId = "live-1",
                CustomerName = "Live",
                LastActivityAt = DateTime.Now
            },
            new AdminChatSession
            {
                SessionId = "trash-1",
                CustomerName = "Deleted",
                LastActivityAt = DateTime.Now,
                IsDeleted = true,
                DeletedAt = DateTime.Now,
                DeletedBy = "admin01"
            }
        );
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        var active = await svc.GetSessionsAsync(false, 60);
        var deleted = await svc.GetSessionsAsync(true, 60);

        Assert.Single(active);
        Assert.Equal("live-1", active[0].SessionId);
        Assert.Single(deleted);
        Assert.Equal("trash-1", deleted[0].SessionId);
        Assert.Single(await svc.GetActiveSessionsAsync(60));
    }

    [Fact]
    public async Task GetUnreadCustomerMessageCountAsync_WhenScopedByBranch_CountsOnlyThatBranch()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.AddRange(
            new AdminChatSession { SessionId = "sess-1", BranchId = 1, CustomerName = "A" },
            new AdminChatSession { SessionId = "sess-2", BranchId = 2, CustomerName = "B" });
        ctx.AdminChatMessages.AddRange(
            new AdminChatMessage { SessionId = "sess-1", Role = "user", Content = "Hello", IsRead = false },
            new AdminChatMessage { SessionId = "sess-2", Role = "user", Content = "Hi", IsRead = false });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        var count = await svc.GetUnreadCustomerMessageCountAsync(1);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetSessionsAsync_Returns_All_NonDeleted_Sessions_From_Database()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.AddRange(
            new AdminChatSession
            {
                SessionId = "recent-1",
                CustomerName = "Recent",
                LastActivityAt = DateTime.Now
            },
            new AdminChatSession
            {
                SessionId = "old-1",
                CustomerName = "Old",
                LastActivityAt = DateTime.Now.AddDays(-10)
            }
        );
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        var sessions = await svc.GetSessionsAsync(false, 30);

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, s => s.SessionId == "recent-1");
        Assert.Contains(sessions, s => s.SessionId == "old-1");
    }

    [Fact]
    public async Task GetSessionsAsync_Backfills_TraceOnly_Sessions_And_Messages()
    {
        var ctx = CreateContext();
        ctx.AIConversationTraces.Add(new AIConversationTrace
        {
            SessionId = "trace-only-1",
            CustomerMessage = "Cho toi xem phong trong",
            FinalAnswer = "Minh gui danh sach phong cho ban",
            CreatedAt = DateTime.Now.AddDays(-3)
        });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        var sessions = await svc.GetSessionsAsync(false, 30);
        var messages = await svc.GetSessionMessagesAsync("trace-only-1");

        Assert.Contains(sessions, s => s.SessionId == "trace-only-1");
        Assert.Collection(messages,
            m => Assert.Equal("user", m.Role),
            m => Assert.Equal("ai", m.Role));
        Assert.Equal("Cho toi xem phong trong", messages[0].Content);
        Assert.Equal("Minh gui danh sach phong cho ban", messages[1].Content);
    }

    [Fact]
    public async Task RestoreSessionAsync_Clears_Delete_Metadata_And_Resurfaces_Session()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "trash-1",
            CustomerName = "Deleted",
            LastActivityAt = DateTime.Now,
            IsDeleted = true,
            DeletedAt = DateTime.Now.AddMinutes(-5),
            DeletedBy = "admin01"
        });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.RestoreSessionAsync("trash-1");

        var session = await ctx.AdminChatSessions.FirstAsync(s => s.SessionId == "trash-1");
        var active = await svc.GetSessionsAsync(false, 60);
        var deleted = await svc.GetSessionsAsync(true, 60);

        Assert.False(session.IsDeleted);
        Assert.Null(session.DeletedAt);
        Assert.Null(session.DeletedBy);
        Assert.Contains(active, s => s.SessionId == "trash-1");
        Assert.DoesNotContain(deleted, s => s.SessionId == "trash-1");
    }

    [Fact]
    public async Task PermanentlyDeleteSessionAsync_Removes_Session_And_Messages()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession
        {
            SessionId = "trash-1",
            CustomerName = "Deleted",
            LastActivityAt = DateTime.Now,
            IsDeleted = true,
            DeletedAt = DateTime.Now,
            DeletedBy = "admin01"
        });
        ctx.AdminChatMessages.AddRange(
            new AdminChatMessage
            {
                SessionId = "trash-1",
                Role = "user",
                Content = "Hello"
            },
            new AdminChatMessage
            {
                SessionId = "trash-1",
                Role = "admin",
                Content = "Hi"
            }
        );
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.PermanentlyDeleteSessionAsync("trash-1");

        Assert.False(await ctx.AdminChatSessions.AnyAsync(s => s.SessionId == "trash-1"));
        Assert.False(await ctx.AdminChatMessages.AnyAsync(m => m.SessionId == "trash-1"));
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

    [Fact]
    public async Task AddAiReplyAsync_Saves_Ai_Message()
    {
        var ctx = CreateContext();
        ctx.AdminChatSessions.Add(new AdminChatSession { SessionId = "sess-1", CustomerName = "Test" });
        await ctx.SaveChangesAsync();
        var svc = new AdminChatService(ctx);

        await svc.AddAiReplyAsync("sess-1", "AI Response", formBlockJson: "{}", formBlockType: "roomCards");

        var msg = await ctx.AdminChatMessages.FirstAsync();
        Assert.Equal("ai", msg.Role);
        Assert.Equal("AI Response", msg.Content);
        Assert.Equal("AI", msg.CreatedBy);
        Assert.Equal("roomCards", msg.FormBlockType);
        Assert.Equal("{}", msg.FormBlockJson);
    }
}
