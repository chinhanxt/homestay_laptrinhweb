using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.AI;
using WebHomestay.Services.AI;
using Xunit;

namespace WebHomestay.Tests.Services;

public class AdminAIStudioConfigServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetAsync_ReturnsStructuredDefaultConfig()
    {
        await using var context = CreateContext();
        var service = new AdminAIStudioConfigService(context);

        var config = await service.GetAsync();

        Assert.Equal("Phong cách trả lời", config.Categories[0]);
        Assert.Equal(6, config.Categories.Count);
        Assert.NotNull(config.AssistantProfile);
        Assert.Contains(config.ConversationFlows, flow => flow.Id == "hourly");
        Assert.Contains(config.ConversationFlows, flow => flow.Id == "daily");
        Assert.Contains(config.FieldDefinitions, field => field.Key == "branchId");
        Assert.Contains(config.UiBlockDefinitions, block => block.Type == "bookingForm");
        Assert.NotNull(config.RuntimePolicies);
        Assert.NotNull(config.Handoff);
        Assert.DoesNotContain(config.Categories, name => name == "Thanh toán / QR");
        Assert.Contains("branch -> room -> slot", config.RuntimePolicies.DisplayRule);
        Assert.False(string.IsNullOrWhiteSpace(config.RuntimePolicies.FallbackReplyRule));
        Assert.False(string.IsNullOrWhiteSpace(config.RuntimePolicies.SessionPersistenceRule));
    }

    [Fact]
    public async Task GetAsync_MigratesLegacyConfig_IntoStructuredConfig()
    {
        await using var context = CreateContext();
        context.SystemSettings.Add(new SystemSetting
        {
            SettingKey = AdminAIStudioConfigService.SettingKey,
            GroupName = "AI",
            SettingValue = """{"categories":["Phong cách & Prompt"],"responseStyle":{"tone":"Cu"},"paymentQrRules":{"paymentRule":"legacy qr"}}"""
        });
        await context.SaveChangesAsync();

        var service = new AdminAIStudioConfigService(context);
        var config = await service.GetAsync();

        Assert.Equal("Cu", config.AssistantProfile.Tone);
        Assert.Equal("Phong cách trả lời", config.Categories[0]);
        Assert.Contains(config.ConversationFlows, flow => flow.Id == "hourly");
        Assert.DoesNotContain(config.Categories, name => name == "Thanh toán / QR");
    }

    [Fact]
    public async Task SaveAsync_PersistsStructuredJsonDocument_WithoutPaymentQrRules()
    {
        await using var context = CreateContext();
        var service = new AdminAIStudioConfigService(context);
        var request = new AdminAIStudioConfigRequest
        {
            Categories = ["Phong cách trả lời"],
            AssistantProfile = new AdminAIAssistantProfile
            {
                Tone = "Rõ ràng, thân thiện",
                RolePrompt = "Base prompt test",
                MissingInfoPrompt = "Hỏi thiếu field"
            }
        };

        await service.SaveAsync(request);

        var setting = await context.SystemSettings.SingleAsync(s => s.SettingKey == AdminAIStudioConfigService.SettingKey);
        var payload = JsonSerializer.Deserialize<AdminAIStudioConfigRequest>(setting.SettingValue);
        Assert.Equal("AI", setting.GroupName);
        Assert.NotNull(payload);
        Assert.Equal("Rõ ràng, thân thiện", payload!.AssistantProfile.Tone);
        Assert.Equal("Base prompt test", payload.AssistantProfile.RolePrompt);
        Assert.DoesNotContain("PaymentQrRules", setting.SettingValue, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paymentQrRules", setting.SettingValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsStructuredConfig()
    {
        await using var context = CreateContext();
        var service = new AdminAIStudioConfigService(context);
        var request = new AdminAIStudioConfigRequest
        {
            Categories = ["Flow hội thoại"],
            AssistantProfile = new AdminAIAssistantProfile
            {
                Tone = "Ngắn gọn"
            },
            ConversationFlows =
            [
                new AdminAIConversationFlow
                {
                    Id = "hourly",
                    Name = "Đặt giờ"
                }
            ],
            FieldDefinitions =
            [
                new AdminAIFieldDefinition
                {
                    Id = "field-branch",
                    Key = "branchId",
                    Label = "Chi nhánh"
                }
            ]
        };

        await service.SaveAsync(request);
        var loaded = await service.GetAsync();

        Assert.Equal("Ngắn gọn", loaded.AssistantProfile.Tone);
        Assert.Single(loaded.ConversationFlows);
        Assert.Equal("hourly", loaded.ConversationFlows[0].Id);
        Assert.Single(loaded.FieldDefinitions);
        Assert.Equal("branchId", loaded.FieldDefinitions[0].Key);
        Assert.NotNull(loaded.LastUpdated);
    }

    [Fact]
    public async Task SaveAsync_SyncsRequiredLegacyRuntimeSettings()
    {
        await using var context = CreateContext();
        var service = new AdminAIStudioConfigService(context);
        var request = new AdminAIStudioConfigRequest
        {
            AssistantProfile = new AdminAIAssistantProfile
            {
                Tone = "Tinh gọn",
                RolePrompt = "Vai trò test",
                MissingInfoPrompt = "Hỏi field còn thiếu"
            }
        };

        await service.SaveAsync(request);

        Assert.Equal("Tinh gọn", (await context.SystemSettings.SingleAsync(s => s.SettingKey == "AIFinalSynthesizerStyle")).SettingValue);
        Assert.Equal("Vai trò test", (await context.SystemSettings.SingleAsync(s => s.SettingKey == "AIFinalBasePrompt")).SettingValue);
        Assert.Equal("Hỏi field còn thiếu", (await context.SystemSettings.SingleAsync(s => s.SettingKey == "AIFinalMissingInfoRule")).SettingValue);
        Assert.False(await context.SystemSettings.AnyAsync(s => s.SettingKey == "AIFinalPaymentRule"));
    }
}
