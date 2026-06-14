using WebHomestay.Models;

namespace WebHomestay.Services;

public interface IAdminChatService
{
    Task<AdminChatSession> UpsertSessionAsync(string sessionId, string? customerName);
    Task<bool> IsPausedAsync(string sessionId);
    Task PauseAsync(string sessionId, string pausedBy);
    Task TakeoverSessionAsync(string sessionId, string takenOverBy);
    Task SoftDeleteSessionAsync(string sessionId, string deletedBy);
    Task ResumeAsync(string sessionId);
    Task<AdminChatMessage> AddAdminReplyAsync(string sessionId, string content, string createdBy,
        string? formBlockJson = null, string? formBlockType = null);
    Task<AdminChatMessage> AddAiReplyAsync(string sessionId, string content,
        string? formBlockJson = null, string? formBlockType = null);
    Task AddCustomerMessageAsync(string sessionId, string content, string? customerName);
    Task AddSystemMessageAsync(string sessionId, string content);
    Task AddSystemAutoReplyAsync(string sessionId);
    Task<List<AdminChatSession>> GetSessionsAsync(bool includeDeleted, int timeoutMinutes = 30);
    Task<List<AdminChatSession>> GetActiveSessionsAsync(int timeoutMinutes = 30);
    Task<List<AdminChatMessage>> GetSessionMessagesAsync(string sessionId);
    Task<int> GetUnreadCustomerMessageCountAsync();
    Task<Dictionary<string, int>> GetUnreadCustomerMessageCountsAsync(IEnumerable<string> sessionIds);
    Task<int> MarkCustomerMessagesReadAsync(string sessionId);
    Task RestoreSessionAsync(string sessionId);
    Task PermanentlyDeleteSessionAsync(string sessionId);
}
