using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Services;

namespace WebHomestay.Hubs;

public class ChatHub : Hub
{
    private readonly IAdminChatService _adminChatService;
    private readonly ApplicationDbContext _context;

    public ChatHub(IAdminChatService adminChatService, ApplicationDbContext context)
    {
        _adminChatService = adminChatService;
        _context = context;
    }

    public async Task JoinSession(string sessionId, string role)
    {
        if (role == "user")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{sessionId}");
        }
        else
        {
            foreach (var group in ChatMonitorScopeHelper.GetMonitorGroupsForViewer(GetSession()))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, group);
            }
        }

        if (role == "admin")
            await SendSessionList(Context.ConnectionId);
    }

    public async Task JoinAdmin()
    {
        foreach (var group in ChatMonitorScopeHelper.GetMonitorGroupsForViewer(GetSession()))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        await SendSessionList(Context.ConnectionId);
    }

    public async Task AdminPause(string sessionId)
    {
        var session = await FindAccessibleSessionAsync(sessionId);
        if (session == null)
        {
            return;
        }

        var adminUser = GetAdminUser();
        await _adminChatService.PauseAsync(sessionId, adminUser);

        await Clients.Groups(ChatMonitorScopeHelper.GetMonitorGroupsForSession(session.BranchId)).SendAsync("sessionUpdate", new
        {
            sessionId,
            status = "paused",
            pausedBy = adminUser,
            lastActivityAt = DateTime.Now,
            totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(GetCurrentBranchScope())
        });
    }

    public async Task AdminResume(string sessionId)
    {
        var session = await FindAccessibleSessionAsync(sessionId);
        if (session == null)
        {
            return;
        }

        await _adminChatService.ResumeAsync(sessionId);

        await Clients.Groups(ChatMonitorScopeHelper.GetMonitorGroupsForSession(session.BranchId)).SendAsync("sessionUpdate", new
        {
            sessionId,
            status = "auto",
            pausedBy = (string?)null,
            lastActivityAt = DateTime.Now,
            totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(GetCurrentBranchScope())
        });
    }

    public async Task AdminReply(string sessionId, string content,
        string? formBlockJson = null, string? formBlockType = null)
    {
        var session = await FindAccessibleSessionAsync(sessionId);
        if (session == null)
        {
            return;
        }

        var adminUser = GetAdminUser();
        var msg = await _adminChatService.AddAdminReplyAsync(sessionId, content,
            adminUser, formBlockJson, formBlockType);

        var replyPayload = new
        {
            sessionId,
            role = "admin",
            content = msg.Content,
            formBlockJson = msg.FormBlockJson,
            formBlockType = msg.FormBlockType,
            createdAt = msg.CreatedAt
        };

        await Clients.Group($"user_{sessionId}").SendAsync("newMessage", replyPayload);
        await Clients.Groups(ChatMonitorScopeHelper.GetMonitorGroupsForSession(session.BranchId)).SendAsync("newMessage", replyPayload);

        await Clients.Groups(ChatMonitorScopeHelper.GetMonitorGroupsForSession(session.BranchId)).SendAsync("sessionUpdate", new
        {
            sessionId,
            status = session?.Status ?? "auto",
            pausedBy = session?.PausedBy,
            pauseReason = session?.PauseReason,
            takenOverBy = session?.TakenOverBy,
            takenOverAt = session?.TakenOverAt,
            lastMessage = content,
            lastActivityAt = msg.CreatedAt,
            totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(GetCurrentBranchScope())
        });
    }

    private string GetAdminUser()
    {
        return Context.GetHttpContext()?.Session.GetString("AdminUser") ?? "unknown";
    }

    private async Task SendSessionList(string connectionId)
    {
        var branchScope = GetCurrentBranchScope();
        if (!CanAccessBranchScopedData())
        {
            await Clients.Client(connectionId).SendAsync("sessionList", new List<object>());
            return;
        }

        var activeSessions = await _adminChatService.GetActiveSessionsAsync(30, branchScope);
        var sessionIds = activeSessions.Select(s => s.SessionId).ToList();
        var unreadCounts = await _adminChatService.GetUnreadCustomerMessageCountsAsync(sessionIds, branchScope);
        var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(branchScope);
        var result = activeSessions.Select(s => new
        {
            sessionId = s.SessionId,
            customerName = s.CustomerName,
            status = s.Status,
            pausedBy = s.PausedBy,
            pauseReason = s.PauseReason,
            takenOverBy = s.TakenOverBy,
            takenOverAt = s.TakenOverAt,
            lastActivityAt = s.LastActivityAt,
            unreadCount = unreadCounts.GetValueOrDefault(s.SessionId),
            totalUnreadCount
        }).ToList();

        await Clients.Client(connectionId).SendAsync("sessionList", result);
    }

    private ISession GetSession()
        => Context.GetHttpContext()!.Session;

    private bool CanAccessBranchScopedData()
        => ChatMonitorScopeHelper.IsSuperAdmin(GetSession()) || GetSession().GetInt32("AdminBranchId").HasValue;

    private int? GetCurrentBranchScope()
        => ChatMonitorScopeHelper.GetScopedBranchId(GetSession());

    private Task<Models.AdminChatSession?> FindAccessibleSessionAsync(string sessionId)
    {
        var query = ChatMonitorScopeHelper.ApplyBranchScope(
            _context.AdminChatSessions.AsNoTracking(),
            GetCurrentBranchScope(),
            ChatMonitorScopeHelper.IsSuperAdmin(GetSession()));

        return query.FirstOrDefaultAsync(session => session.SessionId == sessionId);
    }
}
