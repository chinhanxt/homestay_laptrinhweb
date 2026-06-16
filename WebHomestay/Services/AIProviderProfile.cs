namespace WebHomestay.Services;

public sealed class AIProviderProfile
{
    public string Provider { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(Provider) ? "ai" : Provider;
}
