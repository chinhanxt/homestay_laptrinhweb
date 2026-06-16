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
            if (session.IsDeleted)
            {
                session.IsDeleted = false;
                session.DeletedAt = null;
                session.DeletedBy = null;
                session.Status = "auto";
                session.PausedBy = null;
                session.PausedAt = null;
                session.PauseReason = null;
                session.TakenOverBy = null;
                session.TakenOverAt = null;
            }
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
        session.PauseReason = null;
        session.LastActivityAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task TakeoverSessionAsync(string sessionId, string takenOverBy)
    {
        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null) return;

        var now = DateTime.Now;
        session.Status = "paused";
        session.PausedBy = takenOverBy;
        session.PausedAt = now;
        session.PauseReason = "manual_handoff";
        session.TakenOverBy = takenOverBy;
        session.TakenOverAt = now;
        session.LastActivityAt = now;
        await _db.SaveChangesAsync();
    }

    public async Task SoftDeleteSessionAsync(string sessionId, string deletedBy)
    {
        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null) return;

        var now = DateTime.Now;
        session.IsDeleted = true;
        session.DeletedBy = deletedBy;
        session.DeletedAt = now;
        session.LastActivityAt = now;
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
        session.PauseReason = null;
        session.TakenOverBy = null;
        session.TakenOverAt = null;
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

    public async Task<AdminChatMessage> AddAiReplyAsync(string sessionId, string content,
        string? formBlockJson = null, string? formBlockType = null)
    {
        var msg = new AdminChatMessage
        {
            SessionId = sessionId,
            Role = "ai",
            Content = content,
            FormBlockJson = formBlockJson,
            FormBlockType = formBlockType,
            CreatedBy = "AI",
            CreatedAt = DateTime.Now,
            IsRead = true
        };
        _db.AdminChatMessages.Add(msg);

        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session != null)
            session.LastActivityAt = DateTime.Now;

        await _db.SaveChangesAsync();
        return msg;
    }

    public async Task AddCustomerMessageAsync(string sessionId, string content, string? customerName)
    {
        var msg = new AdminChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = content,
            CreatedBy = customerName,
            CreatedAt = DateTime.Now,
            IsRead = false
        };
        _db.AdminChatMessages.Add(msg);

        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session != null)
            session.LastActivityAt = DateTime.Now;

        await _db.SaveChangesAsync();
    }

    public async Task AddSystemMessageAsync(string sessionId, string content)
    {
        var msg = new AdminChatMessage
        {
            SessionId = sessionId,
            Role = "system",
            Content = content,
            CreatedAt = DateTime.Now,
            IsRead = false
        };
        _db.AdminChatMessages.Add(msg);

        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session != null)
            session.LastActivityAt = DateTime.Now;

        await _db.SaveChangesAsync();
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

    public async Task<List<AdminChatSession>> GetSessionsAsync(bool includeDeleted, int timeoutMinutes = 30)
    {
        await BackfillLegacySessionsAsync();

        var query = _db.AdminChatSessions.AsQueryable();

        query = includeDeleted
            ? query.Where(s => s.IsDeleted)
            : query.Where(s => !s.IsDeleted);

        return await query
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync();
    }

    public async Task<List<AdminChatSession>> GetActiveSessionsAsync(int timeoutMinutes = 30)
    {
        await BackfillLegacySessionsAsync();

        var cutoff = DateTime.Now.AddMinutes(-timeoutMinutes);
        return await _db.AdminChatSessions
            .Where(s => !s.IsDeleted && s.LastActivityAt >= cutoff)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync();
    }

    public async Task<List<AdminChatMessage>> GetSessionMessagesAsync(string sessionId)
    {
        await BackfillLegacyMessagesFromTracesAsync(sessionId);

        return await _db.AdminChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCustomerMessageCountAsync()
    {
        return await _db.AdminChatMessages
            .Where(m => m.Role == "user" && !m.IsRead)
            .Join(_db.AdminChatSessions.Where(s => !s.IsDeleted),
                  m => m.SessionId,
                  s => s.SessionId,
                  (m, s) => m.SessionId)
            .Distinct()
            .CountAsync();
    }

    public async Task<Dictionary<string, int>> GetUnreadCustomerMessageCountsAsync(IEnumerable<string> sessionIds)
    {
        var ids = sessionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, int>();

        return await _db.AdminChatMessages
            .Where(m => ids.Contains(m.SessionId) && m.Role == "user" && !m.IsRead)
            .GroupBy(m => m.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count);
    }

    public async Task<int> MarkCustomerMessagesReadAsync(string sessionId)
    {
        var unread = await _db.AdminChatMessages
            .Where(m => m.SessionId == sessionId && m.Role == "user" && !m.IsRead)
            .ToListAsync();

        foreach (var message in unread)
            message.IsRead = true;

        if (unread.Count > 0)
            await _db.SaveChangesAsync();

        return unread.Count;
    }

    public async Task RestoreSessionAsync(string sessionId)
    {
        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null) return;

        session.IsDeleted = false;
        session.DeletedAt = null;
        session.DeletedBy = null;
        await _db.SaveChangesAsync();
    }

    public async Task PermanentlyDeleteSessionAsync(string sessionId)
    {
        var messages = await _db.AdminChatMessages
            .Where(m => m.SessionId == sessionId)
            .ToListAsync();
        if (messages.Count > 0)
            _db.AdminChatMessages.RemoveRange(messages);

        var session = await _db.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session != null)
            _db.AdminChatSessions.Remove(session);

        if (messages.Count > 0 || session != null)
            await _db.SaveChangesAsync();
    }

    private async Task BackfillLegacySessionsAsync()
    {
        var existingSessionIds = await _db.AdminChatSessions
            .Select(s => s.SessionId)
            .ToListAsync();

        var existingSessionSet = existingSessionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        var messageSummaries = await _db.AdminChatMessages
            .Where(m => !string.IsNullOrWhiteSpace(m.SessionId))
            .GroupBy(m => m.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                CreatedAt = g.Min(m => m.CreatedAt),
                LastActivityAt = g.Max(m => m.CreatedAt),
                CustomerName = g
                    .Where(m => m.Role == "user" && m.CreatedBy != null && m.CreatedBy != "")
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.CreatedBy)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var traceSummaries = await _db.AIConversationTraces
            .Where(t => !string.IsNullOrWhiteSpace(t.SessionId))
            .GroupBy(t => t.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                CreatedAt = g.Min(t => t.CreatedAt),
                LastActivityAt = g.Max(t => t.CreatedAt)
            })
            .ToListAsync();

        var newSessions = new List<AdminChatSession>();

        foreach (var summary in messageSummaries)
        {
            if (existingSessionSet.Contains(summary.SessionId))
            {
                continue;
            }

            newSessions.Add(new AdminChatSession
            {
                SessionId = summary.SessionId,
                CustomerName = summary.CustomerName,
                Status = "auto",
                CreatedAt = summary.CreatedAt,
                LastActivityAt = summary.LastActivityAt
            });
            existingSessionSet.Add(summary.SessionId);
        }

        foreach (var summary in traceSummaries)
        {
            if (existingSessionSet.Contains(summary.SessionId))
            {
                continue;
            }

            newSessions.Add(new AdminChatSession
            {
                SessionId = summary.SessionId,
                Status = "auto",
                CreatedAt = summary.CreatedAt,
                LastActivityAt = summary.LastActivityAt
            });
            existingSessionSet.Add(summary.SessionId);
        }

        if (newSessions.Count > 0)
        {
            _db.AdminChatSessions.AddRange(newSessions);
        }

        var messageSessionSet = messageSummaries
            .Select(x => x.SessionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var traceSummary in traceSummaries)
        {
            if (!messageSessionSet.Contains(traceSummary.SessionId))
            {
                await BackfillLegacyMessagesFromTracesAsync(traceSummary.SessionId, saveChanges: false);
            }
        }

        if (newSessions.Count > 0 || _db.ChangeTracker.HasChanges())
        {
            await _db.SaveChangesAsync();
        }
    }

    private async Task BackfillLegacyMessagesFromTracesAsync(string sessionId, bool saveChanges = true)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var hasMessages = await _db.AdminChatMessages.AnyAsync(m => m.SessionId == sessionId);
        if (hasMessages)
        {
            return;
        }

        var traces = await _db.AIConversationTraces
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        if (traces.Count == 0)
        {
            return;
        }

        var legacyMessages = new List<AdminChatMessage>();
        foreach (var trace in traces)
        {
            if (!string.IsNullOrWhiteSpace(trace.CustomerMessage))
            {
                legacyMessages.Add(new AdminChatMessage
                {
                    SessionId = sessionId,
                    Role = "user",
                    Content = trace.CustomerMessage,
                    CreatedAt = trace.CreatedAt,
                    IsRead = true
                });
            }

            if (!string.IsNullOrWhiteSpace(trace.FinalAnswer))
            {
                legacyMessages.Add(new AdminChatMessage
                {
                    SessionId = sessionId,
                    Role = "ai",
                    Content = trace.FinalAnswer,
                    CreatedBy = "AI",
                    CreatedAt = trace.CreatedAt.AddMilliseconds(1),
                    IsRead = true
                });
            }
        }

        if (legacyMessages.Count == 0)
        {
            return;
        }

        _db.AdminChatMessages.AddRange(legacyMessages);

        if (saveChanges)
        {
            await _db.SaveChangesAsync();
        }
    }
}
