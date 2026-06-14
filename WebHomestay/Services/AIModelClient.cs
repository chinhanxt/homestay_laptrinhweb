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
            var initialProvider = string.IsNullOrWhiteSpace(_options.Provider) ? "groq" : _options.Provider.Trim().ToLowerInvariant();
            
            try
            {
                return await ExecuteWithRetryAsync(initialProvider, request, cancellationToken);
            }
            catch (Exception ex)
            {
                // Fallback chain
                if (initialProvider == "gemini")
                {
                    try
                    {
                        // Fallback to groq
                        return await ExecuteWithRetryAsync("groq", request, cancellationToken);
                    }
                    catch (Exception fallbackEx)
                    {
                        throw new AggregateException($"Both primary ({initialProvider}) and fallback (groq) failed.", ex, fallbackEx);
                    }
                }
                Console.WriteLine("AIModelClient CompleteAsync Exception: " + ex.ToString());
                throw;
            }
        }

        private async Task<AIModelResponse> ExecuteWithRetryAsync(string provider, AIModelRequest request, CancellationToken cancellationToken)
        {
            int maxRetries = 3;
            int delayMs = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await ExecuteCoreAsync(provider, request, cancellationToken);
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

            return await ExecuteCoreAsync(provider, request, cancellationToken);
        }

        private async Task<AIModelResponse> ExecuteCoreAsync(string provider, AIModelRequest request, CancellationToken cancellationToken)
        {
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

        private string ResolveModel(string provider)
        {
            if (!string.IsNullOrWhiteSpace(_options.Model)) return _options.Model;
            return provider switch
            {
                "groq" => "llama-3.3-70b-versatile",
                "openrouter" => "openai/gpt-4o-mini",
                "9router" => "cx/gpt-5.3-codex",
                "gemini" => "gemini-2.0-flash",
                _ => "llama-3.3-70b-versatile"
            };
        }

        private string ResolveEndpoint(string provider)
        {
            if (!string.IsNullOrWhiteSpace(_options.Endpoint)) return _options.Endpoint;
            return provider switch
            {
                "groq" => "https://api.groq.com/openai/v1/chat/completions",
                "openrouter" => "https://openrouter.ai/api/v1/chat/completions",
                "9router" => "http://localhost:20128/v1/chat/completions",
                "gemini" => "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
                _ => throw new InvalidOperationException($"AI provider '{provider}' is not supported.")
            };
        }
    }
}
