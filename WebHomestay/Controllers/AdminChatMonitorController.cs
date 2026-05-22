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
