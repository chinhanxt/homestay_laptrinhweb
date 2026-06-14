using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebHomestay.Controllers;
using WebHomestay.Data;
using WebHomestay.Models.AI;
using WebHomestay.Services.AI;
using Xunit;

namespace WebHomestay.Tests.Controllers;

public class AdminAIControllerTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AdminAIController BuildController(
        ApplicationDbContext? context = null,
        IAdminAIStudioConfigService? studioConfigService = null,
        IAdminAIRuntimeSimulatorService? runtimeSimulatorService = null,
        IAdminAIReseedService? reseedService = null,
        IEmbeddingService? embeddingService = null,
        IOperationalInsightService? operationalInsightService = null)
    {
        context ??= CreateContext();
        studioConfigService ??= Mock.Of<IAdminAIStudioConfigService>();
        runtimeSimulatorService ??= Mock.Of<IAdminAIRuntimeSimulatorService>();
        reseedService ??= Mock.Of<IAdminAIReseedService>();
        embeddingService ??= Mock.Of<IEmbeddingService>();
        operationalInsightService ??= Mock.Of<IOperationalInsightService>();

        return new AdminAIController(context, studioConfigService, runtimeSimulatorService, reseedService, embeddingService, operationalInsightService);
    }

    [Fact]
    public async Task GetStudioConfig_ReturnsStructuredConfig()
    {
        var service = new Mock<IAdminAIStudioConfigService>();
        service.Setup(x => x.GetAsync()).ReturnsAsync(new AdminAIStudioConfigResponse
        {
            Categories = ["Phong cách trả lời"],
            ConversationFlows =
            [
                new AdminAIConversationFlow { Id = "hourly", Name = "Đặt theo giờ" }
            ]
        });

        var controller = BuildController(studioConfigService: service.Object);

        var result = await controller.GetStudioConfig();

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminAIStudioConfigResponse>(ok.Value);
        Assert.NotEmpty(payload.ConversationFlows);
        Assert.Contains("Phong cách trả lời", payload.Categories);
    }

    [Fact]
    public async Task SaveStudioConfig_ReturnsSuccess()
    {
        var service = new Mock<IAdminAIStudioConfigService>();
        var controller = BuildController(studioConfigService: service.Object);
        var request = new AdminAIStudioConfigRequest
        {
            ResponseStyle = new AdminAIStudioResponseStyle
            {
                Tone = "Ngắn gọn"
            }
        };

        var result = await controller.SaveStudioConfig(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        service.Verify(x => x.SaveAsync(request), Times.Once);
    }

    [Fact]
    public async Task ReseedSystemKnowledge_ReturnsServiceResult()
    {
        var reseed = new Mock<IAdminAIReseedService>();
        reseed.Setup(x => x.ReseedAsync()).ReturnsAsync(new AdminAIReseedResult
        {
            CreatedScopes = 1,
            CreatedKnowledgeUnits = 4
        });
        var controller = BuildController(reseedService: reseed.Object);

        var result = await controller.ReseedSystemKnowledge();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<AdminAIReseedResult>(ok.Value);
        reseed.Verify(x => x.ReseedAsync(), Times.Once);
    }

    [Fact]
    public void GetStudioSimulatorPresets_ReturnsRuntimePresets()
    {
        var simulator = new Mock<IAdminAIRuntimeSimulatorService>();
        simulator.Setup(x => x.GetPresets()).Returns(
        [
            new AdminAIRuntimePreset { Id = "new-chat", Label = "Khách mới" }
        ]);
        var controller = BuildController(runtimeSimulatorService: simulator.Object);

        var result = controller.GetStudioSimulatorPresets();

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<AdminAIRuntimePreset>>(ok.Value);
        Assert.Contains(payload, preset => preset.Id == "new-chat");
    }

    [Fact]
    public async Task RunStudioSimulator_UsesCurrentConfigAndState()
    {
        var config = new AdminAIStudioConfigResponse
        {
            ConversationFlows = [new AdminAIConversationFlow { Id = "hourly" }]
        };
        var state = new AdminAIRuntimeState { FlowId = "hourly" };
        var expected = new AdminAIRuntimeSimulationResult
        {
            ConversationState = new AdminAIRuntimeConversationState { Stage = "collect_branch" }
        };
        var configService = new Mock<IAdminAIStudioConfigService>();
        var simulator = new Mock<IAdminAIRuntimeSimulatorService>();
        configService.Setup(x => x.GetAsync()).ReturnsAsync(config);
        simulator.Setup(x => x.SimulateAsync(config, state)).ReturnsAsync(expected);
        var controller = BuildController(studioConfigService: configService.Object, runtimeSimulatorService: simulator.Object);

        var result = await controller.RunStudioSimulator(state);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        simulator.Verify(x => x.SimulateAsync(config, state), Times.Once);
    }

    [Fact]
    public void AdminAIController_DoesNotExposeLegacyPublicBookingRoutes()
    {
        var methods = typeof(AdminAIController)
            .GetMethods()
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("GetPublicBookingConfig", methods);
        Assert.DoesNotContain("SavePublicBookingConfig", methods);
        Assert.DoesNotContain("GetBookingFormConfig", methods);
        Assert.DoesNotContain("SaveBookingFormConfig", methods);
        Assert.DoesNotContain("GetBrainTraces", methods);
        Assert.DoesNotContain("GetBrainTrace", methods);
        Assert.DoesNotContain("TestAgent", methods);
        Assert.DoesNotContain("BrainPreview", methods);
    }
}
