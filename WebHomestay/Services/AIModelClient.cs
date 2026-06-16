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
            var profiles = AIProviderConfigResolver.GetChatProfiles(_options);
            if (profiles.Count == 0)
            {
                throw new InvalidOperationException("Chưa cấu hình Chat API key. Hãy kiểm tra key.md hoặc AIModel:ChatApiKey.");
            }

            Exception? lastError = null;
            for (var index = 0; index < profiles.Count; index++)
            {
                var profile = profiles[index];
                try
                {
                    return await ExecuteWithRetryAsync(profile, request, cancellationToken);
                }
                catch (Exception ex) when (index < profiles.Count - 1 && AIProviderConfigResolver.ShouldFailover(ex.Message))
                {
                    lastError = ex;
                }
            }

            throw lastError ?? new InvalidOperationException("Không thể gọi mô hình AI.");
        }

        private async Task<AIModelResponse> ExecuteWithRetryAsync(AIProviderProfile profile, AIModelRequest request, CancellationToken cancellationToken)
        {
            int maxRetries = 3;
            int delayMs = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await ExecuteCoreAsync(profile, request, cancellationToken);
                }
                catch (HttpRequestException ex) when (i < maxRetries - 1 && ex.StatusCode != System.Net.HttpStatusCode.Unauthorized && ex.StatusCode != System.Net.HttpStatusCode.BadRequest)
                {
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2;
                }
                catch (InvalidOperationException ex) when (i < maxRetries - 1 && ex.Message.Contains("429"))
                {
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2;
                }
            }

            return await ExecuteCoreAsync(profile, request, cancellationToken);
        }

        private async Task<AIModelResponse> ExecuteCoreAsync(AIProviderProfile profile, AIModelRequest request, CancellationToken cancellationToken)
        {
            var provider = profile.Provider;
            var model = profile.Model;

            if (!string.Equals(provider, "gemini", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(provider, "groq", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"AI provider '{provider}' is not supported. Supported providers: gemini, groq.");
            }

            if (string.IsNullOrWhiteSpace(profile.ApiKey))
            {
                throw new InvalidOperationException($"AI provider '{provider}' cần API key. Hãy kiểm tra key.md hoặc cấu hình AIModel.");
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, profile.Endpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.ApiKey);

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
            
            // Handle differences in response formats if needed, but OpenAI-compatible usually matches this
            string content = string.Empty;
            if (json.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentProp))
                {
                    content = contentProp.GetString() ?? string.Empty;
                }
            }

            return new AIModelResponse
            {
                Provider = provider,
                Model = model,
                Content = content,
                IsMock = false
            };
        }
    }
}
