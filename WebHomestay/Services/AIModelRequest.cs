namespace WebHomestay.Services
{
    public class AIModelRequest
    {
        public string SystemPrompt { get; set; } = string.Empty;
        public string UserMessage { get; set; } = string.Empty;
        public decimal Temperature { get; set; } = 0.4m;
        public int MaxTokens { get; set; } = 800;
    }
}
