using Microsoft.Extensions.Options;
using WebHomestay.Services;
using WebHomestay.Services.AI.Compatibility;
using Xunit;

namespace WebHomestay.Tests.Services
{
    public class AIRuntimeSelectorTests
    {
        [Fact]
        public void GetActiveRuntime_DefaultsToLangGraphWhenUnset()
        {
            var options = Options.Create(new AIModelOptions());
            var selector = new AIRuntimeSelector(options);

            var result = selector.GetActiveRuntime();

            Assert.Equal("langgraph", result);
        }

        [Fact]
        public void GetActiveRuntime_ReturnsConfiguredRuntime()
        {
            var options = Options.Create(new AIModelOptions
            {
                ActiveRuntime = "langgraph"
            });
            var selector = new AIRuntimeSelector(options);

            var result = selector.GetActiveRuntime();

            Assert.Equal("langgraph", result);
        }
    }
}
