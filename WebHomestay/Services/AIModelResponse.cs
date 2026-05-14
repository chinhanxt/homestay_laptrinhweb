namespace WebHomestay.Services
{
    public class AIModelResponse
    {
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsMock { get; set; }
    }
}
