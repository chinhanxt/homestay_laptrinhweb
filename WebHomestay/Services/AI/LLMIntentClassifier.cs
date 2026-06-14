using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WebHomestay.Services.AI
{
    public class LLMIntentClassifier
    {
        private readonly IAIModelClient _aiClient;

        public LLMIntentClassifier(IAIModelClient aiClient)
        {
            _aiClient = aiClient;
        }

        public async Task<(string Intent, double Confidence)> ClassifyIntentAsync(string message, CancellationToken cancellationToken = default)
        {
            var systemPrompt = @"Bạn là trợ lý AI chuyên phân loại ý định người dùng trong tin nhắn đặt phòng homestay.
Phân loại tin nhắn thành một trong ba ý định:
1. 'Hourly_Booking': Khách muốn đặt phòng theo giờ hoặc hỏi phòng trống theo giờ (vd: 3 tiếng, 2 giờ, theo giờ, trong ngày, phòng giờ, giờ trống, còn phòng giờ không, quận 7 có phòng giờ trống không).
2. 'Daily_Booking': Khách muốn đặt phòng theo ngày, qua đêm hoặc hỏi phòng trống theo ngày (vd: 2 ngày, qua đêm, ngày mai, từ thứ 6 đến CN, còn phòng đêm nay không).
3. 'Unknown': Khách chỉ chào hỏi, hỏi giá chung chung hoặc ý định không rõ ràng.
Ưu tiên nhận diện 'Hourly_Booking' khi tin nhắn nhắc rõ đến 'giờ', 'khung giờ', 'phòng giờ', 'thuê giờ', kể cả khi khách chưa cung cấp ngày.
Chỉ trả về chuỗi JSON theo đúng định dạng sau, KHÔNG thêm bất kỳ text nào khác:
{ ""intent"": ""Hourly_Booking"" | ""Daily_Booking"" | ""Unknown"", ""confidence"": <số từ 0.0 đến 1.0> }";

            var request = new AIModelRequest
            {
                UserMessage = message,
                SystemPrompt = systemPrompt,
                Temperature = 0.1m, // Low temperature for deterministic classification
                MaxTokens = 150
            };

            try
            {
                var response = await _aiClient.CompleteAsync(request, cancellationToken);
                
                // Clean the response text in case LLM added markdown like ```json ... ```
                var rawContent = response.Content.Trim();
                if (rawContent.StartsWith("```json")) rawContent = rawContent.Substring(7);
                if (rawContent.StartsWith("```")) rawContent = rawContent.Substring(3);
                if (rawContent.EndsWith("```")) rawContent = rawContent.Substring(0, rawContent.Length - 3);
                rawContent = rawContent.Trim();

                using var doc = JsonDocument.Parse(rawContent);
                var root = doc.RootElement;
                
                var intent = root.GetProperty("intent").GetString() ?? "Unknown";
                var confidence = root.GetProperty("confidence").GetDouble();
                
                return (intent, confidence);
            }
            catch
            {
                // Fallback on error
                return ("Unknown", 0.0);
            }
        }
    }
}
