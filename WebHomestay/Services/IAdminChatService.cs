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
