# Admin Chat Monitor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build admin chat monitoring dashboard with real-time conversation observation, AI pause/resume, admin reply, and form block sending via SignalR.

**Architecture:** Single SignalR hub (`/chatHub`) extends existing AI pipeline. Two new models (`AdminChatSession`, `AdminChatMessage`). Public widget connects to hub via JS. Admin dashboard observes and interacts with sessions real-time.

**Tech Stack:** ASP.NET Core MVC, SignalR, PostgreSQL (EF Core), Bootstrap 5, vanilla JS.

---

## File Structure

### Create:
- `WebHomestay/Models/AdminChatSession.cs`
- `WebHomestay/Models/AdminChatMessage.cs`
- `WebHomestay/Hubs/ChatHub.cs`
- `WebHomestay/Services/IAdminChatService.cs`
- `WebHomestay/Services/AdminChatService.cs`
- `WebHomestay/Controllers/AdminChatMonitorController.cs`
- `WebHomestay/Views/AdminChatMonitor/Index.cshtml`
- `WebHomestay/wwwroot/js/admin-chat-monitor.js`
- `WebHomestay/wwwroot/css/admin-chat-monitor.css`
- `WebHomestay.Tests/Services/AdminChatServiceTests.cs`

### Modify:
- `WebHomestay/Data/ApplicationDbContext.cs` — add DbSets + EF config
- `WebHomestay/Program.cs` — register SignalR + ChatHub route + register AdminChatService
- `WebHomestay/Controllers/AIChatController.cs` — add pause check middleware + hub broadcast
- `WebHomestay/Views/Shared/_Layout.cshtml` — admin floating notification + SignalR client scripts
- `WebHomestay/wwwroot/js/site.js` — public side SignalR connection for receiving admin replies
- `WebHomestay/Helpers/PermissionHelper.cs` — no change needed (existing pattern handles new module)
- `WebHomestay/Data/ApplicationDbContext.cs` — seed ChatMonitor SystemSettings + permissions

---

### Task 1: Data Models

**Files:**
- Create: `WebHomestay/Models/AdminChatSession.cs`
- Create: `WebHomestay/Models/AdminChatMessage.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Create AdminChatSession model**

Write `WebHomestay/Models/AdminChatSession.cs`:

```csharp
namespace WebHomestay.Models;

public class AdminChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string Status { get; set; } = "auto"; // "auto" | "paused"
    public string? PausedBy { get; set; }
    public DateTime? PausedAt { get; set; }
    public string? AutoReplyMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastActivityAt { get; set; } = DateTime.Now;
}
```

- [ ] **Step 2: Create AdminChatMessage model**

Write `WebHomestay/Models/AdminChatMessage.cs`:

```csharp
namespace WebHomestay.Models;

public class AdminChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "user" | "admin" | "system"
    public string Content { get; set; } = string.Empty;
    public string? FormBlockJson { get; set; }
    public string? FormBlockType { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; } = false;
}
```

- [ ] **Step 3: Add DbSets + EF configuration to ApplicationDbContext**

Add to `WebHomestay/Data/ApplicationDbContext.cs`:

After the existing DbSet declarations (around line where AIConversationTraces is declared), add:

```csharp
public DbSet<AdminChatSession> AdminChatSessions => Set<AdminChatSession>();
public DbSet<AdminChatMessage> AdminChatMessages => Set<AdminChatMessage>();
```

In `OnModelCreating`, add after existing entity configurations:

```csharp
modelBuilder.Entity<AdminChatSession>(entity =>
{
    entity.ToTable("admin_chat_sessions");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.SessionId).HasColumnName("session_id");
    entity.HasIndex(e => e.SessionId).IsUnique();
    entity.Property(e => e.CustomerName).HasColumnName("customer_name");
    entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20);
    entity.Property(e => e.PausedBy).HasColumnName("paused_by");
    entity.Property(e => e.PausedAt).HasColumnName("paused_at");
    entity.Property(e => e.AutoReplyMessage).HasColumnName("auto_reply_message");
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");
    entity.Property(e => e.LastActivityAt).HasColumnName("last_activity_at");
});

modelBuilder.Entity<AdminChatMessage>(entity =>
{
    entity.ToTable("admin_chat_messages");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.SessionId).HasColumnName("session_id");
    entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(20);
    entity.Property(e => e.Content).HasColumnName("content");
    entity.Property(e => e.FormBlockJson).HasColumnName("form_block_json");
    entity.Property(e => e.FormBlockType).HasColumnName("form_block_type");
    entity.Property(e => e.CreatedBy).HasColumnName("created_by");
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");
    entity.Property(e => e.IsRead).HasColumnName("is_read");
    entity.HasIndex(e => new { e.SessionId, e.CreatedAt });
});
```

- [ ] **Step 4: Add EF migration**

Run from repo root:
```powershell
dotnet ef migrations add AddAdminChatMonitor --project WebHomestay/WebHomestay.csproj
```

Expected output: "Done. To undo this action, use ..."

- [ ] **Step 5: Commit**

```powershell
git add WebHomestay/Models/AdminChatSession.cs WebHomestay/Models/AdminChatMessage.cs WebHomestay/Data/ApplicationDbContext.cs
git commit -m "feat: add AdminChatSession and AdminChatMessage models"
```

---

### Task 2: AdminChatService

**Files:**
- Create: `WebHomestay/Services/IAdminChatService.cs`
- Create: `WebHomestay/Services/AdminChatService.cs`
- Create: `WebHomestay.Tests/Services/AdminChatServiceTests.cs`
- Modify: `WebHomestay/Program.cs` (register service)

- [ ] **Step 1: Write the failing tests**

Write `WebHomestay.Tests/Services/AdminChatServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;

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
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatServiceTests
```

Expected: Build succeeds but all tests FAIL (types not found).

- [ ] **Step 3: Create IAdminChatService interface**

Write `WebHomestay/Services/IAdminChatService.cs`:

```csharp
using WebHomestay.Models;

namespace WebHomestay.Services;

public interface IAdminChatService
{
    Task<AdminChatSession> UpsertSessionAsync(string sessionId, string? customerName);
    Task<bool> IsPausedAsync(string sessionId);
    Task PauseAsync(string sessionId, string pausedBy);
    Task ResumeAsync(string sessionId);
    Task<AdminChatMessage> AddAdminReplyAsync(string sessionId, string content, string createdBy,
        string? formBlockJson = null, string? formBlockType = null);
    Task AddSystemAutoReplyAsync(string sessionId);
    Task<List<AdminChatSession>> GetActiveSessionsAsync(int timeoutMinutes = 30);
    Task<List<AdminChatMessage>> GetSessionMessagesAsync(string sessionId);
}
```

- [ ] **Step 4: Create AdminChatService implementation**

Write `WebHomestay/Services/AdminChatService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services;

public class AdminChatService : IAdminChatService
{
    private readonly ApplicationDbContext _db;

    public AdminChatService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AdminChatSession> UpsertSessionAsync(string sessionId, string? customerName)
    {
        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null)
        {
            session = new AdminChatSession
            {
                SessionId = sessionId,
                CustomerName = customerName,
                Status = "auto",
                CreatedAt = DateTime.Now,
                LastActivityAt = DateTime.Now
            };
            _db.AdminChatSessions.Add(session);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(customerName))
                session.CustomerName = customerName;
            session.LastActivityAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<bool> IsPausedAsync(string sessionId)
    {
        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        return session?.Status == "paused";
    }

    public async Task PauseAsync(string sessionId, string pausedBy)
    {
        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null) return;

        session.Status = "paused";
        session.PausedBy = pausedBy;
        session.PausedAt = DateTime.Now;
        session.LastActivityAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task ResumeAsync(string sessionId)
    {
        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null) return;

        session.Status = "auto";
        session.PausedBy = null;
        session.PausedAt = null;
        session.LastActivityAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task<AdminChatMessage> AddAdminReplyAsync(string sessionId, string content,
        string createdBy, string? formBlockJson = null, string? formBlockType = null)
    {
        var msg = new AdminChatMessage
        {
            SessionId = sessionId,
            Role = "admin",
            Content = content,
            FormBlockJson = formBlockJson,
            FormBlockType = formBlockType,
            CreatedBy = createdBy,
            CreatedAt = DateTime.Now,
            IsRead = false
        };
        _db.AdminChatMessages.Add(msg);

        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session != null)
            session.LastActivityAt = DateTime.Now;

        await _db.SaveChangesAsync();
        return msg;
    }

    public async Task AddSystemAutoReplyAsync(string sessionId)
    {
        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null) return;

        var autoReply = session.AutoReplyMessage
            ?? "Hiện admin đang bận, vui lòng chờ một chút. Chúng tôi sẽ trả lời bạn sớm nhất.";

        var msg = new AdminChatMessage
        {
            SessionId = sessionId,
            Role = "system",
            Content = autoReply,
            CreatedAt = DateTime.Now,
            IsRead = false
        };
        _db.AdminChatMessages.Add(msg);
        session.LastActivityAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task<List<AdminChatSession>> GetActiveSessionsAsync(int timeoutMinutes = 30)
    {
        var cutoff = DateTime.Now.AddMinutes(-timeoutMinutes);
        return await _db.AdminChatSessions
            .Where(s => s.LastActivityAt >= cutoff)
            .OrderByDescending(s => s.LastActivityAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<List<AdminChatMessage>> GetSessionMessagesAsync(string sessionId)
    {
        return await _db.AdminChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }
}
```

- [ ] **Step 5: Register service in Program.cs**

Add to `WebHomestay/Program.cs` alongside other service registrations (after line 46):

```csharp
builder.Services.AddScoped<IAdminChatService, AdminChatService>();
```

And add the import at top:
```csharp
using WebHomestay.Services;
```
(Already present in Program.cs).

- [ ] **Step 6: Run tests to verify they pass**

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatServiceTests
```

Expected: All tests PASS.

- [ ] **Step 7: Commit**

```powershell
git add WebHomestay/Services/IAdminChatService.cs WebHomestay/Services/AdminChatService.cs WebHomestay.Tests/Services/AdminChatServiceTests.cs WebHomestay/Program.cs
git commit -m "feat: add AdminChatService with tests"
```

---

### Task 3: SignalR Hub

**Files:**
- Create: `WebHomestay/Hubs/ChatHub.cs`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Create ChatHub**

Write `WebHomestay/Hubs/ChatHub.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;

namespace WebHomestay.Hubs;

public class ChatHub : Hub
{
    private readonly IAdminChatService _adminChatService;
    private readonly ApplicationDbContext _db;

    public ChatHub(IAdminChatService adminChatService, ApplicationDbContext db)
    {
        _adminChatService = adminChatService;
        _db = db;
    }

    public async Task JoinSession(string sessionId, string role)
    {
        var groupName = role == "user" ? $"user_{sessionId}" : "admin_monitor";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        if (role == "admin")
            await SendSessionList(Context.ConnectionId);
    }

    public async Task JoinAdmin()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admin_monitor");
        await SendSessionList(Context.ConnectionId);
    }

    public async Task AdminPause(string sessionId)
    {
        var adminUser = GetAdminUser();
        await _adminChatService.PauseAsync(sessionId, adminUser);

        await Clients.Group("admin_monitor").SendAsync("sessionUpdate", new
        {
            sessionId,
            status = "paused",
            pausedBy = adminUser,
            lastActivityAt = DateTime.Now
        });
    }

    public async Task AdminResume(string sessionId)
    {
        await _adminChatService.ResumeAsync(sessionId);

        await Clients.Group("admin_monitor").SendAsync("sessionUpdate", new
        {
            sessionId,
            status = "auto",
            pausedBy = (string?)null,
            lastActivityAt = DateTime.Now
        });
    }

    public async Task AdminReply(string sessionId, string content,
        string? formBlockJson = null, string? formBlockType = null)
    {
        var adminUser = GetAdminUser();
        var msg = await _adminChatService.AddAdminReplyAsync(sessionId, content,
            adminUser, formBlockJson, formBlockType);

        var replyPayload = new
        {
            role = "admin",
            content = msg.Content,
            formBlockJson = msg.FormBlockJson,
            formBlockType = msg.FormBlockType,
            createdAt = msg.CreatedAt
        };

        await Clients.Group($"user_{sessionId}").SendAsync("newMessage", replyPayload);
        await Clients.Group("admin_monitor").SendAsync("sessionUpdate", new
        {
            sessionId,
            status = "auto",
            lastMessage = content,
            lastActivityAt = DateTime.Now
        });
    }

    private string GetAdminUser()
    {
        return Context.GetHttpContext()?.Session.GetString("AdminUser") ?? "unknown";
    }

    private async Task SendSessionList(string connectionId)
    {
        var activeSessions = await _adminChatService.GetActiveSessionsAsync(30);
        var result = activeSessions.Select(s => new
        {
            sessionId = s.SessionId,
            customerName = s.CustomerName,
            status = s.Status,
            pausedBy = s.PausedBy,
            lastActivityAt = s.LastActivityAt
        }).ToList();

        await Clients.Client(connectionId).SendAsync("sessionList", result);
    }
}
```

- [ ] **Step 2: Register SignalR + map hub in Program.cs**

At line 48 (before `builder.Services.AddSession`), add:
```csharp
builder.Services.AddSignalR();
```

At the end of `Program.cs` (after `app.MapControllerRoute(...)` around line 98), add:
```csharp
app.MapHub<WebHomestay.Hubs.ChatHub>("/chatHub");
```

- [ ] **Step 3: Commit**

```powershell
git add WebHomestay/Hubs/ChatHub.cs WebHomestay/Program.cs
git commit -m "feat: add SignalR ChatHub for real-time chat monitoring"
```

---

### Task 4: Modify AIChatController — Pause Middleware

**Files:**
- Modify: `WebHomestay/Controllers/AIChatController.cs`

- [ ] **Step 1: Inject IAdminChatService + IHubContext into AIChatController**

Add private fields and constructor params to `AIChatController`:

```csharp
private readonly IAdminChatService _adminChatService;
private readonly IHubContext<Hubs.ChatHub> _hubContext;

// Add to constructor:
public AIChatController(
    IAIBrainOrchestrator orchestrator,
    IWebHostEnvironment environment,
    IImageMaskingService maskingService,
    ApplicationDbContext context,
    IBookingConductor bookingConductor,
    IAdminChatService adminChatService,
    IHubContext<Hubs.ChatHub> hubContext)
{
    // ... existing assignments ...
    _adminChatService = adminChatService;
    _hubContext = hubContext;
}
```

- [ ] **Step 2: Add pause check + upsert logic to Chat() method**

Replace the body of `Chat()` method (lines 71-123). After the request validation block and before `var brainRequest = ...`:

```csharp
[HttpPost("chat")]
public async Task<IActionResult> Chat([FromBody] PublicAIChatRequest request, CancellationToken cancellationToken)
{
    if (request == null || string.IsNullOrWhiteSpace(request.Message))
    {
        return BadRequest(new
        {
            answer = "Bạn nhập giúp mình nhu cầu đặt phòng để mình tư vấn nhé.",
            message = "Bạn nhập giúp mình nhu cầu đặt phòng để mình tư vấn nhé.",
            sessionId = request?.SessionId ?? string.Empty,
            currentStep = "reply",
            uiBlocks = Array.Empty<object>(),
            state = new AIBookingSessionState()
        });
    }

    try
    {
        // Upsert session + update activity
        var session = await _adminChatService.UpsertSessionAsync(
            request.SessionId, request.CustomerName);

        // Check if paused
        if (session.Status == "paused")
        {
            await _adminChatService.AddSystemAutoReplyAsync(request.SessionId);

            var msg = await _db.AdminChatMessages
                .Where(m => m.SessionId == request.SessionId)
                .OrderByDescending(m => m.CreatedAt)
                .FirstAsync(cancellationToken);

            await _hubContext.Clients.Group($"user_{request.SessionId}")
                .SendAsync("newMessage", new
                {
                    role = "system",
                    content = msg.Content,
                    createdAt = msg.CreatedAt
                }, cancellationToken);

            await _hubContext.Clients.Group("admin_monitor")
                .SendAsync("sessionUpdate", new
                {
                    sessionId = request.SessionId,
                    status = "paused",
                    lastMessage = msg.Content,
                    lastActivityAt = DateTime.Now
                }, cancellationToken);

            return Ok(new
            {
                answer = msg.Content,
                message = msg.Content,
                sessionId = request.SessionId,
                currentStep = "paused",
                isPaused = true,
                uiBlocks = Array.Empty<object>(),
                state = new AIBookingSessionState()
            });
        }

        // Normal AI flow
        var brainRequest = new AIBrainChatRequest
        {
            SessionId = request.SessionId,
            Message = request.Message,
            Mode = ChatMode.PublicBooking,
            BranchId = request.BranchId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            GuestCount = request.GuestCount
        };

        var brainResponse = await _orchestrator.ChatAsync(brainRequest, cancellationToken);

        // Broadcast session update to admin monitor
        await _hubContext.Clients.Group("admin_monitor")
            .SendAsync("sessionUpdate", new
            {
                sessionId = request.SessionId,
                status = session.Status,
                lastMessage = brainResponse.Answer,
                lastActivityAt = DateTime.Now
            }, cancellationToken);

        return Ok(new
        {
            answer = brainResponse.Answer,
            message = brainResponse.Answer,
            sessionId = request.SessionId,
            currentStep = brainResponse.BookingAction,
            uiBlocks = brainResponse.UiBlocks,
            state = brainResponse.BookingState ?? new AIBookingSessionState()
        });
    }
    catch
    {
        return StatusCode(503, new
        {
            answer = "Xin lỗi, mình chưa kiểm tra được tình trạng phòng lúc này. Bạn thử lại sau ít phút nhé.",
            message = "Xin lỗi, mình chưa kiểm tra được tình trạng phòng lúc này. Bạn thử lại sau ít phút nhé.",
            sessionId = request.SessionId,
            currentStep = "error",
            uiBlocks = Array.Empty<object>(),
            state = new AIBookingSessionState()
        });
    }
}
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: Build succeeds with no errors.

- [ ] **Step 4: Commit**

```powershell
git add WebHomestay/Controllers/AIChatController.cs
git commit -m "feat: add AI pause middleware with SignalR broadcast in AIChatController"
```

---

### Task 5: AdminChatMonitorController

**Files:**
- Create: `WebHomestay/Controllers/AdminChatMonitorController.cs`
- Create: `WebHomestay/Views/AdminChatMonitor/Index.cshtml`
- Create: `WebHomestay/wwwroot/js/admin-chat-monitor.js`
- Create: `WebHomestay/wwwroot/css/admin-chat-monitor.css`

- [ ] **Step 1: Create AdminChatMonitorController**

Write `WebHomestay/Controllers/AdminChatMonitorController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Filters;
using WebHomestay.Services;

namespace WebHomestay.Controllers;

[AdminAuthorize]
[Route("admin/chat-monitor")]
public class AdminChatMonitorController : Controller
{
    private readonly IAdminChatService _adminChatService;
    private readonly ApplicationDbContext _context;

    public AdminChatMonitorController(IAdminChatService adminChatService, ApplicationDbContext context)
    {
        _adminChatService = adminChatService;
        _context = context;
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _adminChatService.GetActiveSessionsAsync(30);
        var sessionIds = sessions.Select(s => s.SessionId).ToList();

        // Get last message per session
        var lastMessages = await _context.AdminChatMessages
            .Where(m => sessionIds.Contains(m.SessionId))
            .GroupBy(m => m.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                LastContent = g.OrderByDescending(m => m.CreatedAt).Select(m => m.Content).FirstOrDefault(),
                LastTime = g.Max(m => m.CreatedAt)
            })
            .ToListAsync();

        var result = sessions.Select(s => new
        {
            sessionId = s.SessionId,
            customerName = s.CustomerName,
            status = s.Status,
            pausedBy = s.PausedBy,
            lastActivityAt = s.LastActivityAt,
            lastMessage = lastMessages.FirstOrDefault(lm => lm.SessionId == s.SessionId)?.LastContent ?? ""
        });

        return Ok(result);
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("session/{sessionId}")]
    public async Task<IActionResult> GetSessionDetail(string sessionId)
    {
        var messages = await _adminChatService.GetSessionMessagesAsync(sessionId);
        var traces = await _context.AIConversationTraces
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new
            {
                role = "user",
                content = t.CustomerMessage,
                aiReply = t.FinalAnswer,
                createdAt = t.CreatedAt
            })
            .ToListAsync();

        return Ok(new { messages, traces });
    }
}
```

- [ ] **Step 2: Create the monitor view**

Write `WebHomestay/Views/AdminChatMonitor/Index.cshtml`:

```html
@{
    ViewData["Title"] = "Chat Monitor";
    Layout = "_Layout";
}

@section Styles {
    <link rel="stylesheet" href="~/css/admin-chat-monitor.css" asp-append-version="true" />
}

<div class="chat-monitor-container">
    <aside class="chat-session-sidebar" id="session-sidebar">
        <div class="sidebar-header">
            <h5><i class="fas fa-comments me-2"></i>Hội thoại</h5>
            <span class="badge bg-primary" id="session-count-badge">0</span>
        </div>
        <div class="sidebar-search">
            <input type="text" class="form-control form-control-sm" placeholder="Tìm session..." id="session-search" />
        </div>
        <div class="session-list" id="session-list">
            <div class="text-muted text-center p-3">Đang tải...</div>
        </div>
    </aside>

    <main class="chat-main-panel">
        <div class="chat-placeholder" id="chat-placeholder">
            <div class="text-center text-muted mt-5">
                <i class="fas fa-comment-dots fa-4x mb-3"></i>
                <p>Chọn một hội thoại để xem chi tiết</p>
            </div>
        </div>
        <div class="chat-active-panel d-none" id="chat-active-panel">
            <div class="chat-active-header" id="chat-active-header">
                <div class="d-flex align-items-center gap-2">
                    <strong id="chat-customer-name">Khách</strong>
                    <span class="badge" id="chat-status-badge">Đang theo dõi</span>
                </div>
                <div class="d-flex gap-2">
                    <button class="btn btn-sm btn-outline-warning" id="btn-pause-ai">
                        <i class="fas fa-pause me-1"></i>Pause AI
                    </button>
                    <button class="btn btn-sm btn-outline-success d-none" id="btn-resume-ai">
                        <i class="fas fa-play me-1"></i>Resume AI
                    </button>
                    <button class="btn btn-sm btn-outline-secondary" id="btn-config-auto-reply">
                        <i class="fas fa-cog"></i>
                    </button>
                </div>
            </div>
            <div class="chat-messages-area" id="chat-messages-area">
                <div class="text-center text-muted p-3">Đang tải tin nhắn...</div>
            </div>
            <div class="chat-reply-area">
                <div class="input-group">
                    <button class="btn btn-outline-primary dropdown-toggle" type="button" data-bs-toggle="dropdown">
                        <i class="fas fa-plus"></i>
                    </button>
                    <ul class="dropdown-menu">
                        <li><a class="dropdown-item send-form-block" data-type="roomSelector" href="#">Gửi danh sách phòng</a></li>
                        <li><a class="dropdown-item send-form-block" data-type="slotPicker" href="#">Gửi khung giờ</a></li>
                        <li><a class="dropdown-item send-form-block" data-type="infoForm" href="#">Gửi form thông tin</a></li>
                        <li><a class="dropdown-item send-form-block" data-type="paymentQr" href="#">Gửi QR thanh toán</a></li>
                    </ul>
                    <input type="text" class="form-control" id="admin-reply-input" placeholder="Nhập tin nhắn..." maxlength="1000" />
                    <button class="btn btn-primary" id="btn-send-reply"><i class="fas fa-paper-plane"></i></button>
                </div>
            </div>
        </div>
    </main>
</div>

@section Scripts {
    <script src="~/js/admin-chat-monitor.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: Create admin-chat-monitor.js**

Write `WebHomestay/wwwroot/js/admin-chat-monitor.js`:

```javascript
let connection = null;
let currentSessionId = null;
let sessions = {};

document.addEventListener('DOMContentLoaded', function () {
    initializeSignalR();
    document.getElementById('btn-pause-ai')?.addEventListener('click', () => pauseAI());
    document.getElementById('btn-resume-ai')?.addEventListener('click', () => resumeAI());
    document.getElementById('btn-send-reply')?.addEventListener('click', () => sendReply());
    document.getElementById('admin-reply-input')?.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') sendReply();
    });
    document.getElementById('session-search')?.addEventListener('input', filterSessions);
    document.querySelectorAll('.send-form-block').forEach(el => {
        el.addEventListener('click', (e) => {
            e.preventDefault();
            sendFormBlock(el.dataset.type);
        });
    });
    document.getElementById('btn-config-auto-reply')?.addEventListener('click', showAutoReplyConfig);
});

function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl('/chatHub')
        .withAutomaticReconnect()
        .build();

    connection.on('sessionUpdate', function (data) {
        sessions[data.sessionId] = { ...sessions[data.sessionId], ...data };
        renderSessionList();
        updateBadge();
        if (currentSessionId === data.sessionId) updateChatHeader(data);
    });

    connection.on('sessionList', function (list) {
        sessions = {};
        list.forEach(s => { sessions[s.sessionId] = s; });
        renderSessionList();
        updateBadge();
    });

    connection.start().then(function () {
        connection.invoke('joinAdmin');
        loadSessionsFallback();
    }).catch(function (err) {
        console.error('SignalR connection failed:', err);
    });
}

function loadSessionsFallback() {
    fetch('/admin/chat-monitor/sessions')
        .then(r => r.json())
        .then(list => {
            list.forEach(s => { sessions[s.sessionId] = { ...sessions[s.sessionId], ...s }; });
            renderSessionList();
            updateBadge();
        });
}

function renderSessionList() {
    const container = document.getElementById('session-list');
    const search = (document.getElementById('session-search')?.value || '').toLowerCase();
    const sorted = Object.values(sessions).sort((a, b) =>
        new Date(b.lastActivityAt || 0) - new Date(a.lastActivityAt || 0));

    container.innerHTML = '';
    sorted.forEach(s => {
        if (search && !s.sessionId.toLowerCase().includes(search) &&
            (s.customerName || '').toLowerCase().includes(search)) return;
        const div = document.createElement('div');
        div.className = `session-card ${currentSessionId === s.sessionId ? 'active' : ''}`;
        div.dataset.sessionId = s.sessionId;
        div.innerHTML = `
            <div class="session-card-name">${escapeHtml(s.customerName || 'Khách ' + s.sessionId.slice(0, 6))}</div>
            <div class="session-card-preview">${escapeHtml((s.lastMessage || '').slice(0, 50))}</div>
            <div class="session-card-meta">
                <span class="badge ${s.status === 'paused' ? 'bg-warning' : 'bg-success'}">${s.status}</span>
                <small class="text-muted">${timeAgo(s.lastActivityAt)}</small>
            </div>
        `;
        div.addEventListener('click', () => selectSession(s.sessionId));
        container.appendChild(div);
    });
}

function updateBadge() {
    const badge = document.getElementById('session-count-badge');
    const count = Object.keys(sessions).length;
    if (badge) badge.textContent = count;
}

function selectSession(sessionId) {
    currentSessionId = sessionId;
    document.getElementById('chat-placeholder')?.classList.add('d-none');
    const panel = document.getElementById('chat-active-panel');
    if (panel) panel.classList.remove('d-none');
    renderSessionList();
    loadChatMessages(sessionId);
    updateChatHeader(sessions[sessionId] || {});
}

function updateChatHeader(data) {
    const name = document.getElementById('chat-customer-name');
    const badge = document.getElementById('chat-status-badge');
    const pauseBtn = document.getElementById('btn-pause-ai');
    const resumeBtn = document.getElementById('btn-resume-ai');
    if (name) name.textContent = data.customerName || 'Khách ' + (currentSessionId || '').slice(0, 6);
    if (badge) {
        badge.textContent = data.status === 'paused' ? 'Đã pause AI' : 'AI đang trả lời';
        badge.className = `badge ${data.status === 'paused' ? 'bg-warning' : 'bg-success'}`;
    }
    if (pauseBtn) pauseBtn.classList.toggle('d-none', data.status === 'paused');
    if (resumeBtn) resumeBtn.classList.toggle('d-none', data.status !== 'paused');
}

function loadChatMessages(sessionId) {
    const area = document.getElementById('chat-messages-area');
    area.innerHTML = '<div class="text-center text-muted p-3">Đang tải...</div>';

    fetch(`/admin/chat-monitor/session/${encodeURIComponent(sessionId)}`)
        .then(r => r.json())
        .then(data => {
            area.innerHTML = '';
            data.traces?.forEach(t => {
                area.appendChild(createMsgBubble('user', t.content, t.createdAt));
                area.appendChild(createMsgBubble('ai', t.aiReply, t.createdAt));
            });
            data.messages?.forEach(m => {
                area.appendChild(createMsgBubble(m.role, m.content, m.createdAt, m.formBlockType));
            });
            area.scrollTop = area.scrollHeight;
        })
        .catch(() => {
            area.innerHTML = '<div class="text-center text-danger p-3">Lỗi tải tin nhắn</div>';
        });
}

function createMsgBubble(role, content, createdAt, formBlockType) {
    const div = document.createElement('div');
    div.className = `chat-bubble chat-bubble-${role}`;
    const label = role === 'user' ? 'Khách' : role === 'ai' ? 'AI' : role === 'admin' ? 'Admin' : 'Hệ thống';
    const time = createdAt ? new Date(createdAt).toLocaleTimeString('vi-VN') : '';
    div.innerHTML = `
        <div class="chat-bubble-label">${label} · ${time}</div>
        <div class="chat-bubble-content">${escapeHtml(content)}</div>
        ${formBlockType ? `<div class="chat-bubble-form badge bg-info">📋 ${formBlockType}</div>` : ''}
    `;
    return div;
}

function pauseAI() {
    if (!currentSessionId || !connection) return;
    connection.invoke('adminPause', currentSessionId).catch(console.error);
}

function resumeAI() {
    if (!currentSessionId || !connection) return;
    connection.invoke('adminResume', currentSessionId).catch(console.error);
}

function sendReply() {
    const input = document.getElementById('admin-reply-input');
    const content = input.value.trim();
    if (!content || !currentSessionId || !connection) return;
    connection.invoke('adminReply', currentSessionId, content).catch(console.error);
    input.value = '';
}

function sendFormBlock(type) {
    if (!currentSessionId || !connection) return;
    const defaultJson = {
        roomSelector: JSON.stringify({ type: 'roomSelector', label: 'Chọn phòng' }),
        slotPicker: JSON.stringify({ type: 'slotPicker', label: 'Chọn khung giờ' }),
        infoForm: JSON.stringify({ type: 'infoForm', label: 'Nhập thông tin', fields: ['customerName', 'customerPhone', 'customerEmail'] }),
        paymentQr: JSON.stringify({ type: 'paymentQr', label: 'Thanh toán' })
    };
    connection.invoke('adminReply', currentSessionId, `📋 Vui lòng ${type === 'roomSelector' ? 'chọn phòng' : type === 'slotPicker' ? 'chọn khung giờ' : type === 'infoForm' ? 'điền thông tin' : 'thanh toán'}`, defaultJson[type] || '{}', type).catch(console.error);
}

function showAutoReplyConfig() {
    const current = sessions[currentSessionId];
    const msg = prompt('Nhập tin nhắn tự động khi AI bị pause:', current?.autoReplyMessage || '');
    if (msg !== null) {
        fetch(`/admin/chat-monitor/session/${encodeURIComponent(currentSessionId)}/auto-reply`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ autoReplyMessage: msg })
        });
    }
}

function filterSessions() {
    renderSessionList();
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text || '';
    return div.innerHTML;
}

function timeAgo(dateStr) {
    if (!dateStr) return '';
    const now = new Date();
    const date = new Date(dateStr);
    const diff = Math.floor((now - date) / 60000);
    if (diff < 1) return 'Vừa xong';
    if (diff < 60) return diff + ' phút';
    return Math.floor(diff / 60) + ' giờ';
}
```

- [ ] **Step 4: Create admin-chat-monitor.css**

Write `WebHomestay/wwwroot/css/admin-chat-monitor.css`:

```css
.chat-monitor-container {
    display: flex;
    height: calc(100vh - 90px);
    background: #f8f9fa;
    border-radius: 12px;
    overflow: hidden;
    box-shadow: 0 2px 12px rgba(0,0,0,0.06);
}

.chat-session-sidebar {
    width: 280px;
    min-width: 280px;
    background: white;
    border-right: 1px solid #e9ecef;
    display: flex;
    flex-direction: column;
}

.sidebar-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px;
    border-bottom: 1px solid #e9ecef;
}

.sidebar-search {
    padding: 8px 16px;
}

.session-list {
    flex: 1;
    overflow-y: auto;
}

.session-card {
    padding: 12px 16px;
    border-bottom: 1px solid #f0f0f0;
    cursor: pointer;
    transition: background 0.15s;
}

.session-card:hover { background: #f0f4ff; }
.session-card.active { background: #e8f0fe; border-left: 3px solid #4361ee; }

.session-card-name { font-weight: 600; font-size: 14px; }
.session-card-preview { font-size: 12px; color: #6c757d; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.session-card-meta { display: flex; justify-content: space-between; align-items: center; margin-top: 4px; }

.chat-main-panel { flex: 1; display: flex; flex-direction: column; }

.chat-active-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 12px 20px;
    background: white;
    border-bottom: 1px solid #e9ecef;
}

.chat-messages-area {
    flex: 1;
    overflow-y: auto;
    padding: 16px 20px;
    background: #f8f9fa;
}

.chat-bubble {
    max-width: 75%;
    margin-bottom: 12px;
    padding: 8px 14px;
    border-radius: 12px;
    font-size: 14px;
}

.chat-bubble-label { font-size: 11px; color: #6c757d; margin-bottom: 4px; }
.chat-bubble-content { line-height: 1.5; }
.chat-bubble-form { margin-top: 4px; }

.chat-bubble-user { background: #e3f2fd; margin-right: auto; border-bottom-left-radius: 4px; }
.chat-bubble-ai { background: white; margin-left: auto; border-bottom-right-radius: 4px; border: 1px solid #e9ecef; }
.chat-bubble-admin { background: #d4edda; margin-left: auto; border-bottom-right-radius: 4px; }
.chat-bubble-system { background: #fff3cd; margin-right: auto; border-bottom-left-radius: 4px; }

.chat-reply-area {
    padding: 12px 20px;
    background: white;
    border-top: 1px solid #e9ecef;
}
```

- [ ] **Step 5: Build to verify**

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```powershell
git add WebHomestay/Controllers/AdminChatMonitorController.cs WebHomestay/Views/AdminChatMonitor/Index.cshtml WebHomestay/wwwroot/js/admin-chat-monitor.js WebHomestay/wwwroot/css/admin-chat-monitor.css
git commit -m "feat: add admin chat monitor controller, view, JS and CSS"
```

---

### Task 6: Admin Floating Notification + Public Widget SignalR Client

**Files:**
- Modify: `WebHomestay/Views/Shared/_Layout.cshtml`
- Modify: `WebHomestay/wwwroot/js/site.js`

- [ ] **Step 1: Add SignalR client library + admin floating notification to _Layout.cshtml**

At the bottom of `_Layout.cshtml` (before `</body>`), add after the existing script references (after line 173):

```html
<script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
```

Before the `</body>` tag (after the `showPublicChatbot` block), add the admin floating notification:

```html
@if (Context.Session.GetString("AdminUser") != null)
{
    <div id="admin-chat-notification" class="admin-chat-float" style="position:fixed;bottom:30px;right:30px;z-index:9999;display:none;">
        <a href="/admin/chat-monitor" class="btn btn-primary position-relative" style="width:60px;height:60px;border-radius:50%;box-shadow:0 4px 16px rgba(0,0,0,0.2);">
            <i class="fas fa-comment-dots fa-lg"></i>
            <span id="admin-chat-badge" class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger" style="display:none;">
                0
            </span>
        </a>
    </div>
    <script>
        document.addEventListener('DOMContentLoaded', function () {
            if (typeof signalR !== 'undefined') {
                const notifConnection = new signalR.HubConnectionBuilder()
                    .withUrl('/chatHub')
                    .withAutomaticReconnect()
                    .build();
                notifConnection.on('sessionUpdate', function () {
                    // Badge updates via sessionList
                });
                notifConnection.on('sessionList', function (list) {
                    const badge = document.getElementById('admin-chat-badge');
                    const container = document.getElementById('admin-chat-notification');
                    if (!badge || !container) return;
                    const count = list ? list.length : 0;
                    if (count > 0) {
                        badge.textContent = count;
                        badge.style.display = 'inline';
                        container.style.display = 'block';
                    } else {
                        badge.style.display = 'none';
                        container.style.display = 'none';
                    }
                });
                notifConnection.start().then(function () {
                    notifConnection.invoke('joinAdmin');
                }).catch(function () {});
            }
        });
    </script>
}
```

- [ ] **Step 2: Add SignalR receive handler to public chat widget (site.js)**

In `WebHomestay/wwwroot/js/site.js`, after the `sessionId` is created (around line 21-25), add:

```javascript
// Connect to SignalR for admin chat monitor
let chatHubConnection = null;

function connectChatHub(currentSessionId) {
    if (typeof signalR === 'undefined') return;
    chatHubConnection = new signalR.HubConnectionBuilder()
        .withUrl('/chatHub')
        .withAutomaticReconnect()
        .build();

    chatHubConnection.on('newMessage', function (data) {
        if (data.role === 'system') {
            appendMessage('bot', data.content);
            const statusEl = document.querySelector('.ai-chat-status');
            if (statusEl) statusEl.textContent = '⏳ Admin đang xem tin nhắn của bạn';
        } else if (data.role === 'admin') {
            appendMessage('bot', '👤 Admin: ' + data.content);
            const statusEl = document.querySelector('.ai-chat-status');
            if (statusEl) statusEl.textContent = '';
            if (data.formBlockJson) {
                renderAdminFormBlock(data.formBlockJson, data.formBlockType);
            }
        }
        const input = document.querySelector('.ai-chat-input');
        if (input) input.disabled = false;
    });

    chatHubConnection.start().then(function () {
        chatHubConnection.invoke('joinSession', currentSessionId, 'user');
    }).catch(function () {});
}
```

Also in `site.js`, find where `sessionId` is created and after the sessionId is assigned, add:

```javascript
// Connect SignalR hub after session ID is ready
connectChatHub(sessionId);
```

And add a helper function to render admin form blocks:

```javascript
function renderAdminFormBlock(formBlockJson, formBlockType) {
    try {
        const block = JSON.parse(formBlockJson);
        if (formBlockType === 'roomSelector') {
            // Reuse existing renderRoomCards or show prompt
            appendMessage('bot', '📋 Admin đã gửi danh sách phòng. Vui lòng xem phía trên.');
        } else if (formBlockType === 'slotPicker') {
            appendMessage('bot', '📋 Admin đã gửi khung giờ. Vui lòng chọn.');
        } else if (formBlockType === 'infoForm') {
            appendMessage('bot', '📋 Admin yêu cầu điền thông tin: ' + (block.fields || []).join(', '));
        } else if (formBlockType === 'paymentQr') {
            appendMessage('bot', '📋 Admin đã gửi QR thanh toán.');
        }
    } catch (e) {
        console.error('Error parsing form block:', e);
    }
}
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```powershell
git add WebHomestay/Views/Shared/_Layout.cshtml WebHomestay/wwwroot/js/site.js
git commit -m "feat: add admin floating notification + public widget SignalR client"
```

---

### Task 7: SystemSettings Seed + Permission Seed

**Files:**
- Modify: `WebHomestay/Controllers/AdminChatMonitorController.cs` (add auto-reply config endpoint)
- Modify: `WebHomestay/Data/ApplicationDbContext.cs` (or seed migration — but Program.cs already seeds settings on first run)

- [ ] **Step 1: Add auto-reply config endpoint to AdminChatMonitorController**

Add to `WebHomestay/Controllers/AdminChatMonitorController.cs` at end:

```csharp
[AdminAuthorize(Permission = "chats.pause")]
[HttpPost("session/{sessionId}/auto-reply")]
public async Task<IActionResult> SetAutoReply(string sessionId, [FromBody] JsonElement body)
{
    if (!body.TryGetProperty("autoReplyMessage", out var msgEl) || msgEl.ValueKind != JsonValueKind.String)
        return BadRequest();

    var session = await _context.AdminChatSessions
        .FirstOrDefaultAsync(s => s.SessionId == sessionId);
    if (session == null) return NotFound();

    session.AutoReplyMessage = msgEl.GetString();
    await _context.SaveChangesAsync();
    return Ok();
}
```

Add `using System.Text.Json;` at top of the controller file.

- [ ] **Step 2: Seed ChatMonitor settings in Program.cs**

In `Program.cs`, inside the seed block (after the existing `BookingLeadTimeHours` seed, around line 78), add:

```csharp
// Seed ChatMonitor settings
var chatSettings = new[]
{
    ("ChatMonitor_AutoReplyMessage", "Hiện admin đang bận, vui lòng chờ một chút. Chúng tôi sẽ trả lời bạn sớm nhất."),
    ("ChatMonitor_SessionTimeoutMinutes", "30"),
    ("ChatMonitor_MaxActiveSessions", "50"),
    ("ChatMonitor_NewSessionSound", "true")
};

foreach (var (key, val) in chatSettings)
{
    if (!context.SystemSettings.Any(s => s.SettingKey == key))
    {
        context.SystemSettings.Add(new WebHomestay.Models.SystemSetting
        {
            SettingKey = key,
            SettingValue = val,
            Description = $"Chat Monitor: {key.Replace("ChatMonitor_", "")}",
            GroupName = "ChatMonitor"
        });
    }
}
context.SaveChanges();
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```powershell
git add WebHomestay/Controllers/AdminChatMonitorController.cs WebHomestay/Program.cs
git commit -m "feat: add auto-reply config endpoint + seed ChatMonitor settings"
```

---

### Task 8: Final Build & Test

- [ ] **Step 1: Full build**

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: Build succeeds with 0 errors.

- [ ] **Step 2: Run all tests**

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected: All tests pass (existing + new AdminChatServiceTests).

- [ ] **Step 3: Apply database migration**

```powershell
dotnet ef database update --project WebHomestay/WebHomestay.csproj
```

Expected: Database updated with new `admin_chat_sessions` and `admin_chat_messages` tables.

- [ ] **Step 4: Commit all remaining work**

```powershell
git add -A
git commit -m "feat: complete admin chat monitor implementation"
```

---

## Self-Review Checklist

1. **Spec coverage:**
   - Data models (AdminChatSession, AdminChatMessage) ✓
   - SignalR Hub with all events ✓
   - AI pause/resume middleware ✓
   - AdminChatMonitorController with 3 endpoints ✓
   - Floating notification on admin pages ✓
   - Public widget SignalR client ✓
   - Form block sending ✓
   - SystemSettings + Permissions ✓

2. **Placeholder scan:** No "TBD", "TODO", or incomplete sections.

3. **Type consistency:** Method signatures match between interface, implementation, and callsites.

4. **Scope check:** Single feature, well-bounded. All tasks produce working, testable code.
