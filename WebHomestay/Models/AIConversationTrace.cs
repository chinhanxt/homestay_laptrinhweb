namespace WebHomestay.Models
{
    public class AIConversationTrace
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string SessionId { get; set; } = string.Empty;
        public string CustomerMessage { get; set; } = string.Empty;
        public string PersonaSummary { get; set; } = string.Empty;
        public string LiveSystemSnapshot { get; set; } = string.Empty;
        public string RetrievedKnowledgeJson { get; set; } = "[]";
        public string GraphReasoningJson { get; set; } = "[]";
        public string GuardResult { get; set; } = string.Empty;
        public string FinalAnswer { get; set; } = string.Empty;
        public string ModelProvider { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
