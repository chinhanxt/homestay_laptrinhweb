namespace WebHomestay.Services
{
    public class BookingFormConfigRequest
    {
        public string? FormSchema { get; set; }
    }

    public class FinalSynthesizerConfigRequest
    {
        public string? Style { get; set; }
        public string? FormSchema { get; set; }
        public string? ConditionOptions { get; set; }
        public string? BasePrompt { get; set; }
        public string? LanguageRule { get; set; }
        public string? DataTruthRule { get; set; }
        public string? MissingInfoRule { get; set; }
        public string? BookingRule { get; set; }
        public string? FormRule { get; set; }
        public string? PaymentRule { get; set; }
        public string? MemoryRule { get; set; }
        public string? ContextFormatRule { get; set; }
    }

    public class PublicBookingConfigRequest
    {
        public string? Prompt { get; set; }
        public string? TriggerWords { get; set; }
        public string? MaxTokens { get; set; }
        public string? Timeout { get; set; }
    }
}
