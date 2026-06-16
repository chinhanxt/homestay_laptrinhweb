namespace WebHomestay.Services
{
    public class AIModelOptions
    {
        public string Provider { get; set; } = "gemini";
        public string ApiKey { get; set; } = string.Empty;
        public string ChatApiKey { get; set; } = string.Empty;
        public string EmbeddingApiKey { get; set; } = string.Empty;
        public string ChatFallbackProvider { get; set; } = string.Empty;
        public string ChatFallbackApiKey { get; set; } = string.Empty;
        public string ChatFallbackModel { get; set; } = string.Empty;
        public string ChatFallbackEndpoint { get; set; } = string.Empty;
        public string EmbeddingFallbackProvider { get; set; } = string.Empty;
        public string EmbeddingFallbackApiKey { get; set; } = string.Empty;
        public string EmbeddingFallbackModel { get; set; } = string.Empty;
        public string EmbeddingFallbackEndpoint { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string ActiveRuntime { get; set; } = "langgraph";
    }
}
