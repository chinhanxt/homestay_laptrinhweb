namespace WebHomestay.Services
{
    public class AIModelOptions
    {
        public string Provider { get; set; } = "groq";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
    }
}
