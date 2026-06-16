namespace WebHomestay.Services.AI.Workflow;

public sealed class WorkflowState
{
    public string SessionId { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public List<string> StageLog { get; } = new();
    public float[]? QueryEmbedding { get; set; }
    public string RetrievalJson { get; set; } = "[]";
    public string GraphJson { get; set; } = "{}";
    public string BookingAction { get; set; } = "reply";
    public List<object> UiBlocks { get; set; } = new();
    public string FinalAnswer { get; set; } = string.Empty;
}
