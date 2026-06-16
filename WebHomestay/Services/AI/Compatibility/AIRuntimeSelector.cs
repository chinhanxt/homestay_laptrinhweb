using Microsoft.Extensions.Options;

namespace WebHomestay.Services.AI.Compatibility;

public sealed class AIRuntimeSelector : IAIRuntimeSelector
{
    private readonly IOptions<AIModelOptions> _options;

    public AIRuntimeSelector(IOptions<AIModelOptions> options)
    {
        _options = options;
    }

    public string GetActiveRuntime()
        => _options.Value.ActiveRuntime;
}
