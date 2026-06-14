using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WebHomestay.Services.AI
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly AIModelOptions _options;
        private readonly ILogger<EmbeddingService> _logger;

        public EmbeddingService(
            HttpClient httpClient,
            IOptions<AIModelOptions> options,
            ILogger<EmbeddingService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return GetDeterministicMockEmbedding(string.Empty);
            }

            var provider = string.IsNullOrWhiteSpace(_options.Provider) ? "groq" : _options.Provider.Trim().ToLowerInvariant();
            
            // If the provider is groq, we fall back to mock since Groq doesn't support embeddings.
            if (provider == "groq" || provider == "mock" || string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _logger.LogInformation("Using deterministic mock embedding for provider: {Provider}", provider);
                return GetDeterministicMockEmbedding(text);
            }

            try
            {
                string endpoint;
                string model;

                if (provider == "gemini")
                {
                    endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/v1/embeddings";
                    // Try to check if override is present
                    var overrideEndpoint = Environment.GetEnvironmentVariable("AI_ENDPOINT_OVERRIDE");
                    if (!string.IsNullOrWhiteSpace(overrideEndpoint))
                    {
                        // Parse embeddings endpoint from completions endpoint
                        endpoint = overrideEndpoint.Replace("/chat/completions", "/embeddings");
                    }
                    model = "text-embedding-004";
                }
                else if (provider == "openrouter")
                {
                    endpoint = "https://openrouter.ai/api/v1/embeddings";
                    model = "openai/text-embedding-3-small";
                }
                else
                {
                    // Fallback to OpenAI-compatible generic
                    endpoint = string.IsNullOrWhiteSpace(_options.Endpoint) 
                        ? "https://api.openai.com/v1/embeddings" 
                        : _options.Endpoint.Replace("/chat/completions", "/embeddings");
                    model = "text-embedding-3-small";
                }

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

                var payload = new JsonObject
                {
                    ["model"] = model,
                    ["input"] = text
                };

                httpRequest.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Embedding API failed with status {Status}: {Error}. Falling back to mock.", response.StatusCode, errorBody);
                    return GetDeterministicMockEmbedding(text);
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (json.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                {
                    var firstData = data[0];
                    if (firstData.TryGetProperty("embedding", out var embeddingProp))
                    {
                        var embeddingList = new System.Collections.Generic.List<float>();
                        foreach (var val in embeddingProp.EnumerateArray())
                        {
                            embeddingList.Add((float)val.GetDouble());
                        }

                        // Ensure vector has exactly 1536 dimension by padding or truncating if needed
                        var result = embeddingList.ToArray();
                        if (result.Length == 1536)
                        {
                            return result;
                        }
                        
                        _logger.LogWarning("Embedding size returned was {Length} instead of 1536. Resizing...", result.Length);
                        return ResizeVector(result, 1536);
                    }
                }

                _logger.LogWarning("Embedding API response structure invalid. Falling back to mock.");
                return GetDeterministicMockEmbedding(text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get embedding from API. Falling back to mock.");
                return GetDeterministicMockEmbedding(text);
            }
        }

        /// <summary>
        /// Generates a deterministic mock embedding of size 1536 based on text hashing.
        /// This ensures the application runs and compiles locally without external dependencies.
        /// </summary>
        private float[] GetDeterministicMockEmbedding(string text)
        {
            var vector = new float[1536];
            if (string.IsNullOrEmpty(text))
            {
                vector[0] = 1.0f; // Unit vector
                return vector;
            }

            // Simple deterministic generation based on text segments
            var hashSeed = 17;
            foreach (var c in text)
            {
                hashSeed = hashSeed * 31 + c;
            }

            var random = new Random(hashSeed);
            double sumOfSquares = 0;

            for (int i = 0; i < 1536; i++)
            {
                // Generate values between -1.0 and 1.0
                double value = random.NextDouble() * 2.0 - 1.0;
                
                // Add some keyword characteristics to mock semantic grouping
                // Words starting with similar characters or having similar lengths will have slightly correlated vectors
                if (text.Length > 0 && i % 10 == 0)
                {
                    value += (text[i % text.Length] - 96) / 26.0;
                }

                vector[i] = (float)value;
                sumOfSquares += value * value;
            }

            // Normalize vector to unit length (L2 norm) so CosineDistance works correctly
            float norm = (float)Math.Sqrt(sumOfSquares);
            if (norm > 0)
            {
                for (int i = 0; i < 1536; i++)
                {
                    vector[i] /= norm;
                }
            }

            return vector;
        }

        private float[] ResizeVector(float[] original, int targetSize)
        {
            var resized = new float[targetSize];
            int sizeToCopy = Math.Min(original.Length, targetSize);
            Array.Copy(original, resized, sizeToCopy);

            // Normalize again
            double sumOfSquares = 0;
            for (int i = 0; i < targetSize; i++)
            {
                sumOfSquares += resized[i] * resized[i];
            }
            float norm = (float)Math.Sqrt(sumOfSquares);
            if (norm > 0)
            {
                for (int i = 0; i < targetSize; i++)
                {
                    resized[i] /= norm;
                }
            }
            return resized;
        }
    }
}
