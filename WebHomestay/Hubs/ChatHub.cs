using Microsoft.AspNetCore.SignalR;
using WebHomestay.Services;

namespace WebHomestay.Hubs;

public class ChatHub : Hub
{
    private readonly IAdminChatService _adminChatService;

    public ChatHub(IAdminChatService adminChatService)
    {
        _adminChatService = adminChatService;
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

        await Clients.Group($"user_{sessionId}").SendAsync("newMessage", new
        {
            role = "admin",
            content = msg.Content,
            formBlockJson = msg.FormBlockJson,
            formBlockType = msg.FormBlockType,
            createdAt = msg.CreatedAt
        });

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
