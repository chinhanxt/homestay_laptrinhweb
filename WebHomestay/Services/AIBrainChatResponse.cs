namespace WebHomestay.Services
{
    public class AIBrainChatResponse
    {
        public Guid TraceId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public string PersonaSummary { get; set; } = string.Empty;
        public string GuardResult { get; set; } = string.Empty;
        public string ModelProvider { get; set; } = string.Empty;
        public bool IsMock { get; set; }
        public string FormSchema { get; set; } = "[]";
    }
}
