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
                throw new EmbeddingUnavailableException("Cannot generate embedding for empty text.");
            }

            var profiles = AIProviderConfigResolver.GetEmbeddingProfiles(_options);
            if (profiles.Count == 0)
            {
                throw new EmbeddingUnavailableException("AI embedding API key is missing.");
            }

            Exception? lastError = null;
            for (var index = 0; index < profiles.Count; index++)
            {
                var profile = profiles[index];

                try
                {
                    return await ExecuteEmbeddingRequestAsync(profile, text, cancellationToken);
                }
                catch (EmbeddingUnavailableException ex) when (index < profiles.Count - 1 && AIProviderConfigResolver.ShouldFailover(ex.Message))
                {
                    lastError = ex;
                }
            }

            if (lastError is EmbeddingUnavailableException embeddingError)
            {
                throw embeddingError;
            }

            throw new EmbeddingUnavailableException("Failed to get embedding from configured providers.");
        }

        private async Task<float[]> ExecuteEmbeddingRequestAsync(AIProviderProfile profile, string text, CancellationToken cancellationToken)
        {
            if (!string.Equals(profile.Provider, "gemini", StringComparison.OrdinalIgnoreCase))
            {
                throw new EmbeddingUnavailableException(
                    $"Provider '{profile.Provider}' is not supported for embeddings. Supported provider: gemini.");
            }

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, profile.Endpoint);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.ApiKey);

                var payload = new JsonObject
                {
                    ["model"] = string.IsNullOrWhiteSpace(profile.Model) ? "gemini-embedding-001" : profile.Model,
                    ["input"] = text,
                    ["dimensions"] = 1536
                };

                httpRequest.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Embedding API failed with status {Status}: {Error}.", response.StatusCode, errorBody);
                    throw new EmbeddingUnavailableException(
                        $"Embedding request failed for provider '{profile.Provider}'. Status: {response.StatusCode}, Error: {errorBody}");
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

                        var result = embeddingList.ToArray();
                        if (result.Length != 1536)
                        {
                            throw new EmbeddingUnavailableException(
                                $"Embedding size {result.Length} does not match required size 1536.");
                        }

                        return result;
                    }
                }

                throw new EmbeddingUnavailableException(
                    $"Failed to deserialize embedding response from provider '{profile.Provider}'.");
            }
            catch (EmbeddingUnavailableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get embedding from API.");
                throw new EmbeddingUnavailableException(
                    $"Failed to get embedding from provider '{profile.Provider}'.",
                    ex);
            }
        }
    }
}
