using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using WebHomestay.Models.AI;

namespace WebHomestay.Services.AI;

public class ConversationManager : IConversationManager
{
    private readonly IMemoryCache _cache;
    private readonly IAIModelClient _modelClient;
    private const int MaxHistorySize = 50;
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(30);

    public ConversationManager(IMemoryCache cache, IAIModelClient modelClient)
    {
        _cache = cache;
        _modelClient = modelClient;
    }

    public Task<AIBookingSessionState> GetOrCreateStateAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"ai-session:{sessionId}";

        if (_cache.TryGetValue<AIBookingSessionState>(cacheKey, out var state) && state != null)
        {
            // Update last access time (TTL refresh implicitly handled by setting it again, or we can just rewrite)
            _cache.Set(cacheKey, state, SessionTtl);
            return Task.FromResult(state);
        }

        var newState = new AIBookingSessionState
        {
            ConversationHistory = new List<ConversationTurn>()
        };

        _cache.Set(cacheKey, newState, SessionTtl);
        return Task.FromResult(newState);
    }

    public Task UpdateStateAsync(string sessionId, AIBookingSessionState state, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"ai-session:{sessionId}";
        _cache.Set(cacheKey, state, SessionTtl);
        return Task.CompletedTask;
    }

    public async Task<List<ConversationTurn>> GetHistoryAsync(string sessionId, int maxTurns = 50, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateStateAsync(sessionId, cancellationToken);
        return state.ConversationHistory.TakeLast(maxTurns).ToList();
    }

    public async Task AddTurnAsync(string sessionId, string userMessage, string aiResponse, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateStateAsync(sessionId, cancellationToken);
        
        state.ConversationHistory.Add(new ConversationTurn
        {
            Timestamp = DateTime.UtcNow,
            UserMessage = userMessage,
            AIResponse = aiResponse
        });

        // Enforce max history size
        if (state.ConversationHistory.Count > MaxHistorySize)
        {
            state.ConversationHistory = state.ConversationHistory.Skip(state.ConversationHistory.Count - MaxHistorySize).ToList();
        }

        await UpdateStateAsync(sessionId, state, cancellationToken);
    }

    public async Task<T?> ResolveReferenceAsync<T>(string sessionId, string reference, int lookbackTurns = 5) where T : class
    {
        var history = await GetHistoryAsync(sessionId, lookbackTurns, cancellationToken: default);
        
        var historyText = string.Join("\n", history.Select(h => $"- Khách: {h.UserMessage}\n  AI: {h.AIResponse}"));
        
        var prompt = $@"Trong {lookbackTurns} tin nhắn gần nhất, khách hàng đề cập '{reference}'. 
Tìm thực thể được tham chiếu từ lịch sử:
{historyText}

Trả về định dạng JSON hợp lệ tương thích với schema sau (chỉ trả về JSON, không kèm markdown codeblock):";

        var request = new AIModelRequest
        {
            SystemPrompt = prompt,
            UserMessage = "Resolve the reference and return JSON.",
            Temperature = 0.1m,
            MaxTokens = 300
        };

        try
        {
            var response = await _modelClient.CompleteAsync(request, default);
            if (!string.IsNullOrWhiteSpace(response.Content))
            {
                var content = response.Content.Trim();
                if (content.StartsWith("```json")) content = content.Substring(7);
                if (content.StartsWith("```")) content = content.Substring(3);
                if (content.EndsWith("```")) content = content.Substring(0, content.Length - 3);
                
                return JsonSerializer.Deserialize<T>(content.Trim(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch
        {
            // If LLM fails to parse, return null
            return null;
        }

        return null;
    }
}
