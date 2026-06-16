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
            state.FinalAnswer = CleanThoughtBlock(response.Content);
            _logger.LogInformation("Response composed for session {SessionId} using provider {Provider}",
                state.SessionId, response.Provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Response composition failed for session {SessionId}", state.SessionId);
            state.FinalAnswer = "Hiện tại hệ thống AI đang có sự cố, vui lòng thử lại sau.";
        }
    }

    private static string CleanThoughtBlock(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;
        
        var thoughtStart = content.IndexOf("<thought>", StringComparison.OrdinalIgnoreCase);
        if (thoughtStart >= 0)
        {
            var thoughtEnd = content.IndexOf("</thought>", thoughtStart, StringComparison.OrdinalIgnoreCase);
            if (thoughtEnd >= 0)
            {
                var cleaned = content.Substring(0, thoughtStart) + content.Substring(thoughtEnd + 10);
                return CleanThoughtBlock(cleaned).Trim();
            }
        }
        
        return content.Trim();
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

        if (state.RetrievalJson != "[]" && state.RetrievalJson.Contains("RoomId"))
        {
            builder.AppendLine("ĐÃ TÌM THẤY PHÒNG PHÙ HỢP: Các phòng này đã được hệ thống tự động tải và hiển thị dưới dạng các thẻ phòng (UI room cards) cho khách hàng trong khung chat.");
            builder.AppendLine("Nhiệm vụ của bạn:");
            builder.AppendLine("- Giới thiệu ngắn gọn các phòng được tìm thấy trong [THÔNG TIN CHÍNH SÁCH & ĐỊA ĐIỂM], nhấn mạnh các đặc điểm phù hợp với yêu cầu của khách hàng (lãng mạn, bồn tắm, ban công, yên tĩnh...).");
            builder.AppendLine("- Hướng dẫn khách hàng chọn và bấm trực tiếp vào thẻ phòng hiển thị bên dưới để xem chi tiết và tiến hành đặt phòng.");
            builder.AppendLine("- Tuyệt đối KHÔNG bắt buộc khách hàng phải cung cấp ngày nhận/trả phòng hoặc chi nhánh trước khi giới thiệu các căn phòng này. Hãy đề xuất các phòng có sẵn này trước, sau đó hỏi ngày/giờ nếu khách muốn kiểm tra trạng thái phòng trống thực tế hoặc book phòng.");
        }
        else
        {
            builder.AppendLine("Nếu thiếu thông tin (như ngày đặt phòng, số lượng khách, chi nhánh), hãy lịch sự hỏi lại khách hàng.");
        }

        return builder.ToString();
    }
}
