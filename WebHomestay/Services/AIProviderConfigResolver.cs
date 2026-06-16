using System;
using System.Collections.Generic;

namespace WebHomestay.Services;

public static class AIProviderConfigResolver
{
    public static List<AIProviderProfile> GetChatProfiles(AIModelOptions options)
    {
        var profiles = new List<AIProviderProfile>();

        var primaryProvider = NormalizeProvider(options.Provider, "gemini");
        var primaryApiKey = FirstNonEmpty(options.ChatApiKey, options.ApiKey);
        if (!string.IsNullOrWhiteSpace(primaryApiKey))
        {
            profiles.Add(new AIProviderProfile
            {
                Provider = primaryProvider,
                ApiKey = primaryApiKey,
                Model = ResolveChatModel(primaryProvider, options.Model),
                Endpoint = ResolveChatEndpoint(primaryProvider, options.Endpoint)
            });
        }

        var fallbackProvider = NormalizeProvider(options.ChatFallbackProvider, string.Empty);
        if (!string.IsNullOrWhiteSpace(fallbackProvider) && !string.IsNullOrWhiteSpace(options.ChatFallbackApiKey))
        {
            profiles.Add(new AIProviderProfile
            {
                Provider = fallbackProvider,
                ApiKey = options.ChatFallbackApiKey,
                Model = ResolveChatModel(fallbackProvider, options.ChatFallbackModel),
                Endpoint = ResolveChatEndpoint(fallbackProvider, options.ChatFallbackEndpoint)
            });
        }

        return profiles;
    }

    public static List<AIProviderProfile> GetEmbeddingProfiles(AIModelOptions options)
    {
        var profiles = new List<AIProviderProfile>();

        var primaryProvider = NormalizeProvider(options.Provider, "gemini");
        var primaryApiKey = FirstNonEmpty(options.EmbeddingApiKey, options.ApiKey);
        if (!string.IsNullOrWhiteSpace(primaryApiKey))
        {
            profiles.Add(new AIProviderProfile
            {
                Provider = primaryProvider,
                ApiKey = primaryApiKey,
                Model = ResolveEmbeddingModel(primaryProvider, string.Empty),
                Endpoint = ResolveEmbeddingEndpoint(primaryProvider, options.Endpoint)
            });
        }

        var fallbackProvider = NormalizeProvider(options.EmbeddingFallbackProvider, string.Empty);
        if (!string.IsNullOrWhiteSpace(fallbackProvider) && !string.IsNullOrWhiteSpace(options.EmbeddingFallbackApiKey))
        {
            profiles.Add(new AIProviderProfile
            {
                Provider = fallbackProvider,
                ApiKey = options.EmbeddingFallbackApiKey,
                Model = ResolveEmbeddingModel(fallbackProvider, options.EmbeddingFallbackModel),
                Endpoint = ResolveEmbeddingEndpoint(fallbackProvider, options.EmbeddingFallbackEndpoint)
            });
        }

        return profiles;
    }

    public static bool ShouldFailover(string? message)
    {
        var lower = (message ?? string.Empty).ToLowerInvariant();
        return lower.Contains("429")
            || lower.Contains("toomanyrequests")
            || lower.Contains("resource_exhausted")
            || lower.Contains("quota")
            || lower.Contains("rate limit")
            || lower.Contains("rate-limit")
            || lower.Contains("unauthorized")
            || lower.Contains("401");
    }

    private static string NormalizeProvider(string? provider, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(provider) ? fallback : provider.Trim().ToLowerInvariant();
        return value switch
        {
            "gemma4" => "gemini",
            _ => value
        };
    }

    private static string ResolveChatModel(string provider, string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        return provider switch
        {
            "groq" => "llama-3.3-70b-versatile",
            _ => "gemini-2.5-flash"
        };
    }

    private static string ResolveChatEndpoint(string provider, string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        return provider switch
        {
            "groq" => "https://api.groq.com/openai/v1/chat/completions",
            _ => "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"
        };
    }

    private static string ResolveEmbeddingModel(string provider, string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        return provider switch
        {
            "gemini" => "gemini-embedding-001",
            _ => "gemini-embedding-001"
        };
    }

    private static string ResolveEmbeddingEndpoint(string provider, string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Replace("/chat/completions", "/embeddings", StringComparison.OrdinalIgnoreCase);
        }

        return provider switch
        {
            "gemini" => "https://generativelanguage.googleapis.com/v1beta/openai/embeddings",
            _ => "https://generativelanguage.googleapis.com/v1beta/openai/embeddings"
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
