using System.Text.Json;

namespace WebHomestay.Services;

public interface IAdminChatQuickSendService
{
    Task<AdminChatQuickSendSchema?> GetSchemaAsync(string type, int? allowedBranchId = null, CancellationToken cancellationToken = default);
    Task<AdminChatQuickSendBuildResult?> BuildAsync(string sessionId, string type, JsonElement payload, int? allowedBranchId = null, CancellationToken cancellationToken = default);
}

public sealed class AdminChatQuickSendSchema
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SubmitLabel { get; set; } = "Gửi cho khách";
    public IReadOnlyList<object> Fields { get; set; } = Array.Empty<object>();
}

public sealed class AdminChatQuickSendBuildResult
{
    public string Message { get; set; } = string.Empty;
    public string FormBlockType { get; set; } = "uiBlocks";
    public IReadOnlyList<object> UiBlocks { get; set; } = Array.Empty<object>();
}
