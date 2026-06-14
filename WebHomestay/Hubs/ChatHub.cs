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
            lastActivityAt = DateTime.Now,
            totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync()
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
            lastActivityAt = DateTime.Now,
            totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync()
        });
    }

    public async Task AdminReply(string sessionId, string content,
        string? formBlockJson = null, string? formBlockType = null)
    {
        var adminUser = GetAdminUser();
        var msg = await _adminChatService.AddAdminReplyAsync(sessionId, content,
            adminUser, formBlockJson, formBlockType);
        var session = await _context.AdminChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

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
        await Clients.Group("admin_monitor").SendAsync("newMessage", replyPayload);

        await Clients.Group("admin_monitor").SendAsync("sessionUpdate", new
        {
            sessionId,
            status = session?.Status ?? "auto",
            pausedBy = session?.PausedBy,
            pauseReason = session?.PauseReason,
            takenOverBy = session?.TakenOverBy,
            takenOverAt = session?.TakenOverAt,
            lastMessage = content,
            lastActivityAt = msg.CreatedAt,
            totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync()
        });
    }

    private string GetAdminUser()
    {
        return Context.GetHttpContext()?.Session.GetString("AdminUser") ?? "unknown";
    }

    private async Task SendSessionList(string connectionId)
    {
        var activeSessions = await _adminChatService.GetActiveSessionsAsync(30);
        var sessionIds = activeSessions.Select(s => s.SessionId).ToList();
        var unreadCounts = await _adminChatService.GetUnreadCustomerMessageCountsAsync(sessionIds);
        var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync();
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
}
