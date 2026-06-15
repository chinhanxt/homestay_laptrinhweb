namespace WebHomestay.Services.AI.Workflow.Nodes;

public sealed class ResponseComposerNode : IWorkflowNode
{
    private readonly IAIModelClient _modelClient;
    private readonly ILogger<ResponseComposerNode> _logger;

    public ResponseComposerNode(
        IAIModelClient modelClient,
        ILogger<ResponseComposerNode> logger)
    {
        _modelClient = modelClient;
        _logger = logger;
    }

    public string Name => "composition";

    public async Task ExecuteAsync(WorkflowState state, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(state.FinalAnswer))
        {
            _logger.LogInformation("Skipping composition for session {SessionId}: answer already set", state.SessionId);
            return;
        }

        try
        {
            var systemPrompt = BuildSystemPrompt(state);

            var request = new AIModelRequest
            {
                SystemPrompt = systemPrompt,
                UserMessage = state.UserMessage,
                Temperature = 0.35m,
                MaxTokens = 800
            };

            var response = await _modelClient.CompleteAsync(request, cancellationToken);
            state.FinalAnswer = response.Content;
            _logger.LogInformation("Response composed for session {SessionId} using provider {Provider}",
                state.SessionId, response.Provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Response composition failed for session {SessionId}", state.SessionId);
            state.FinalAnswer = "Hiện tại hệ thống AI đang có sự cố, vui lòng thử lại sau.";
        }
    }

    private static string BuildSystemPrompt(WorkflowState state)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Bạn là trợ lý AI thông minh cho homestay.");

        if (state.RetrievalJson != "[]")
        {
            builder.AppendLine();
            builder.AppendLine($"[THÔNG TIN CHÍNH SÁCH & ĐỊA ĐIỂM]");
            builder.AppendLine(state.RetrievalJson);
        }

        if (state.GraphJson != "{}")
        {
            builder.AppendLine();
            builder.AppendLine($"[THÔNG TIN ĐỒ THỊ]");
            builder.AppendLine(state.GraphJson);
        }

        builder.AppendLine();
        builder.AppendLine("Luôn trả lời bằng tiếng Việt, thân thiện, tự nhiên.");
        builder.AppendLine("Không tự ý thông báo giá, không xác nhận đặt phòng.");
        builder.AppendLine("Nếu thiếu thông tin, hãy hỏi lại khách hàng.");

        return builder.ToString();
    }
}
