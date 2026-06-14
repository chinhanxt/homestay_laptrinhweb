namespace WebHomestay.Models.AI
{
    public class FinalSynthesizerPromptConfig
    {
        public string Style { get; set; } = string.Empty;
        public string BasePrompt { get; set; } = string.Empty;
        public string LanguageRule { get; set; } = string.Empty;
        public string DataTruthRule { get; set; } = string.Empty;
        public string MissingInfoRule { get; set; } = string.Empty;
        public string BookingRule { get; set; } = string.Empty;
        public string FormRule { get; set; } = string.Empty;
        public string PaymentRule { get; set; } = string.Empty;
        public string MemoryRule { get; set; } = string.Empty;
        public string ContextFormatRule { get; set; } = string.Empty;
        public string PublicBookingPrompt { get; set; } = string.Empty;
        public string PublicBookingPersonality { get; set; } = string.Empty;
    }
}
