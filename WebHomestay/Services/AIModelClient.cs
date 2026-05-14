using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace WebHomestay.Services
{
    public class AIModelClient : IAIModelClient
    {
        private readonly HttpClient _httpClient;
        private readonly AIModelOptions _options;

        public AIModelClient(HttpClient httpClient, IOptions<AIModelOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<AIModelResponse> CompleteAsync(AIModelRequest request, CancellationToken cancellationToken = default)
        {
            var provider = string.IsNullOrWhiteSpace(_options.Provider) ? "groq" : _options.Provider.Trim().ToLowerInvariant();
            var model = ResolveModel(provider);

            if (provider == "mock")
            {
                throw new InvalidOperationException("Mock AI đã bị tắt. Hãy cấu hình Groq API key trong key.md hoặc AIModel:ApiKey.");
            }

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException($"AI provider '{provider}' cần API key. Hãy kiểm tra file key.md hoặc cấu hình AIModel:ApiKey.");
            }

            var endpoint = ResolveEndpoint(provider);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var payload = new JsonObject
            {
                ["model"] = model,
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
                    new JsonObject { ["role"] = "user", ["content"] = request.UserMessage }
                },
                ["temperature"] = (double)request.Temperature,
                ["max_tokens"] = request.MaxTokens
            };

            httpRequest.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"AI provider '{provider}' returned {(int)response.StatusCode}: {errorBody}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var content = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            return new AIModelResponse
            {
                Provider = provider,
                Model = model,
                Content = content,
                IsMock = false
            };
        }

        private string ResolveModel(string provider)
        {
            if (!string.IsNullOrWhiteSpace(_options.Model)) return _options.Model;
            return provider switch
            {
                "groq" => "llama-3.3-70b-versatile",
                "openrouter" or "9router" => "openai/gpt-4o-mini",
                _ => "llama-3.3-70b-versatile"
            };
        }

        private string ResolveEndpoint(string provider)
        {
            if (!string.IsNullOrWhiteSpace(_options.Endpoint)) return _options.Endpoint;
            return provider switch
            {
                "groq" => "https://api.groq.com/openai/v1/chat/completions",
                "openrouter" or "9router" => "https://openrouter.ai/api/v1/chat/completions",
                _ => throw new InvalidOperationException($"AI provider '{provider}' is not supported.")
            };
        }
    }
}
