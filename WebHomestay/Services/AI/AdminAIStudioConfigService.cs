using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.AI;

namespace WebHomestay.Services.AI;

public class AdminAIStudioConfigService : IAdminAIStudioConfigService
{
    public const string SettingKey = "AIStudioConfig";
    private const string Description = "Unified Admin AI studio configuration";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ApplicationDbContext _context;

    public AdminAIStudioConfigService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminAIStudioConfigResponse> GetAsync()
    {
        var setting = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKey);

        if (setting == null || string.IsNullOrWhiteSpace(setting.SettingValue))
        {
            var defaultConfig = BuildDefaultConfig();
            await ApplyLegacyFallbacksAsync(defaultConfig);
            return defaultConfig;
        }

        var config = DeserializeConfig(setting.SettingValue);
        if (config == null)
        {
            return BuildDefaultConfig();
        }

        EnsureStructuredShape(config);
        config.LastUpdated = setting.LastUpdated;
        return config;
    }

    public async Task SaveAsync(AdminAIStudioConfigRequest request)
    {
        var config = NormalizeRequest(request);
        var payload = JsonSerializer.Serialize(config, JsonOptions);
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == SettingKey);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                SettingKey = SettingKey,
                GroupName = "AI",
                Description = Description
            };
            _context.SystemSettings.Add(setting);
        }

        setting.SettingValue = payload;
        setting.GroupName = "AI";
        setting.Description = Description;
        setting.LastUpdated = DateTime.UtcNow;

        await SyncLegacySettingsAsync(config);
        await _context.SaveChangesAsync();
    }

    private async Task ApplyLegacyFallbacksAsync(AdminAIStudioConfigResponse config)
    {
        var settings = await _context.SystemSettings
            .AsNoTracking()
            .Where(s => s.GroupName == "AI")
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

        config.AssistantProfile.Tone = GetSetting(settings, "AIFinalSynthesizerStyle", config.AssistantProfile.Tone);
        config.AssistantProfile.RolePrompt = GetSetting(settings, "AIFinalBasePrompt", config.AssistantProfile.RolePrompt);
        config.AssistantProfile.MissingInfoPrompt = GetSetting(settings, "AIFinalMissingInfoRule", config.AssistantProfile.MissingInfoPrompt);
        config.AssistantProfile.SafetyPrompt = GetSetting(settings, "AIFinalDataTruthRule", config.AssistantProfile.SafetyPrompt);
        config.RuntimePolicies.AutoShowRooms = GetBoolSetting(settings, "AIPublicBookingAutoShowRooms", config.RuntimePolicies.AutoShowRooms);
        config.RuntimePolicies.MaxRoomShows = GetIntSetting(settings, "AIPublicBookingMaxRoomShows", config.RuntimePolicies.MaxRoomShows);
        config.RuntimePolicies.RoomCooldownTurns = GetIntSetting(settings, "AIPublicBookingRoomCooldown", config.RuntimePolicies.RoomCooldownTurns);

        // Load trigger and exit rules
        config.RuntimePolicies.TriggerWords = GetSetting(settings, "AIPublicBookingTriggerWords", config.RuntimePolicies.TriggerWords);
        config.RuntimePolicies.ExitKeywords = GetSetting(settings, "AIPublicBookingExitKeywords", config.RuntimePolicies.ExitKeywords);
        config.RuntimePolicies.ProactiveMode = GetSetting(settings, "AIPublicBookingProactiveMode", config.RuntimePolicies.ProactiveMode);
        config.RuntimePolicies.DependencyRule = GetSetting(settings, "AIPublicBookingDependencyRule", config.RuntimePolicies.DependencyRule);
        config.RuntimePolicies.GuestOverflowRule = GetSetting(settings, "AIPublicBookingGuestOverflowRule", config.RuntimePolicies.GuestOverflowRule);

        // Load form fields config
        if (config.BookingFormFields == null || config.BookingFormFields.Count == 0)
        {
            var schemaJson = GetSetting(settings, "AIBookingFormSchema", string.Empty);
            if (!string.IsNullOrWhiteSpace(schemaJson))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<BookingFormFieldConfig>>(schemaJson, JsonOptions);
                    if (parsed != null)
                    {
                        foreach (var p in parsed) p.Enabled = true;
                        config.BookingFormFields = parsed;
                    }
                }
                catch { }
            }

            if (config.BookingFormFields == null || config.BookingFormFields.Count == 0)
            {
                config.BookingFormFields = new List<BookingFormFieldConfig>
                {
                    new() { Id = "customerName", Label = "Họ và tên", Type = "text", Enabled = true, Required = true, HelpText = "Nhập đúng họ tên người đặt phòng.", Order = 1 },
                    new() { Id = "customerPhone", Label = "Số điện thoại", Type = "tel", Enabled = true, Required = true, HelpText = "Số điện thoại/Zalo để homestay liên hệ xác nhận.", Order = 2 },
                    new() { Id = "customerEmail", Label = "Email", Type = "email", Enabled = true, Required = false, HelpText = "Email nhận thông tin đặt phòng nếu có.", Order = 3 },
                    new() { Id = "customerNote", Label = "Ghi chú khách hàng", Type = "textarea", Enabled = false, Required = false, HelpText = "Các lưu ý đặc biệt khác.", Order = 4 }
                };
            }
        }
    }

    private async Task SyncLegacySettingsAsync(AdminAIStudioConfig request)
    {
        await UpsertLegacySettingAsync("AIFinalSynthesizerStyle", request.AssistantProfile?.Tone ?? string.Empty, "Phong cách trả lời AI");
        await UpsertLegacySettingAsync("AIFinalBasePrompt", request.AssistantProfile?.RolePrompt ?? string.Empty, "Vai trò AI");
        await UpsertLegacySettingAsync("AIFinalMissingInfoRule", request.AssistantProfile?.MissingInfoPrompt ?? string.Empty, "Quy tắc hỏi thông tin thiếu");
        await UpsertLegacySettingAsync("AIFinalDataTruthRule", request.AssistantProfile?.SafetyPrompt ?? string.Empty, "Quy tắc an toàn dữ liệu");
        await UpsertLegacySettingAsync("AIPublicBookingAutoShowRooms", request.RuntimePolicies.AutoShowRooms.ToString().ToLowerInvariant(), "Tự động gợi ý phòng");
        await UpsertLegacySettingAsync("AIPublicBookingMaxRoomShows", request.RuntimePolicies.MaxRoomShows.ToString(), "Số lần tối đa show phòng");
        await UpsertLegacySettingAsync("AIPublicBookingRoomCooldown", request.RuntimePolicies.RoomCooldownTurns.ToString(), "Cooldown show phòng");
        await UpsertLegacySettingAsync("AIPublicBookingPersonality", request.AssistantProfile?.Tone ?? string.Empty, "Tính cách public booking AI");
        await UpsertLegacySettingAsync("AIPublicBookingPrompt", BuildPublicBookingPrompt(request), "Prompt public booking từ Studio");

        // Sync new properties
        await UpsertLegacySettingAsync("AIPublicBookingTriggerWords", request.RuntimePolicies.TriggerWords ?? string.Empty, "Từ khoá kích hoạt đặt phòng");
        await UpsertLegacySettingAsync("AIPublicBookingExitKeywords", request.RuntimePolicies.ExitKeywords ?? string.Empty, "Từ khoá thoát đặt phòng");
        await UpsertLegacySettingAsync("AIPublicBookingProactiveMode", request.RuntimePolicies.ProactiveMode ?? string.Empty, "Chế độ chủ động");
        await UpsertLegacySettingAsync("AIPublicBookingDependencyRule", request.RuntimePolicies.DependencyRule ?? string.Empty, "Quy tắc cha con public booking");
        await UpsertLegacySettingAsync("AIPublicBookingGuestOverflowRule", request.RuntimePolicies.GuestOverflowRule ?? string.Empty, "Quy tắc khi số khách vượt sức chứa");

        // Sync form schema to AIBookingFormSchema
        if (request.BookingFormFields != null)
        {
            var activeFields = request.BookingFormFields
                .Where(f => f.Enabled)
                .Select(f => new
                {
                    id = f.Id,
                    type = f.Type,
                    label = f.Label,
                    required = f.Required,
                    helpText = f.HelpText,
                    order = f.Order
                })
                .OrderBy(f => f.order)
                .ToList();
            var serializedSchema = JsonSerializer.Serialize(activeFields, JsonOptions);
            await UpsertLegacySettingAsync("AIBookingFormSchema", serializedSchema, "Cấu hình Form thông tin đặt phòng");
        }
    }

    private async Task UpsertLegacySettingAsync(string key, string value, string description)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
        if (setting == null)
        {
            _context.SystemSettings.Add(new SystemSetting
            {
                SettingKey = key,
                SettingValue = value,
                GroupName = "AI",
                Description = description,
                LastUpdated = DateTime.UtcNow
            });
            return;
        }

        setting.SettingValue = value;
        setting.GroupName = "AI";
        setting.Description = description;
        setting.LastUpdated = DateTime.UtcNow;
    }

    private static AdminAIStudioConfigResponse? DeserializeConfig(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (HasProperty(document.RootElement, "assistantProfile") ||
                HasProperty(document.RootElement, "conversationFlows") ||
                HasProperty(document.RootElement, "fieldDefinitions"))
            {
                return JsonSerializer.Deserialize<AdminAIStudioConfigResponse>(json, JsonOptions);
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return MigrateLegacyConfig(json);
    }

    private static AdminAIStudioConfigResponse? MigrateLegacyConfig(string json)
    {
        var legacy = JsonSerializer.Deserialize<LegacyAdminAIStudioConfig>(json, JsonOptions);
        if (legacy == null)
        {
            return null;
        }

        var config = BuildDefaultConfig();
        if (!string.IsNullOrWhiteSpace(legacy.ResponseStyle?.Tone))
        {
            config.AssistantProfile.Tone = legacy.ResponseStyle.Tone;
        }

        if (!string.IsNullOrWhiteSpace(legacy.ResponseStyle?.BasePrompt))
        {
            config.AssistantProfile.RolePrompt = legacy.ResponseStyle.BasePrompt;
        }

        if (!string.IsNullOrWhiteSpace(legacy.MemoryRules?.MissingInfoRule))
        {
            config.AssistantProfile.MissingInfoPrompt = legacy.MemoryRules.MissingInfoRule;
        }

        if (!string.IsNullOrWhiteSpace(legacy.SafetyRules?.DataTruthRule))
        {
            config.AssistantProfile.SafetyPrompt = legacy.SafetyRules.DataTruthRule;
        }

        if (legacy.RoomDisplayRules != null)
        {
            config.RuntimePolicies.AutoShowRooms = legacy.RoomDisplayRules.AutoShow;
            config.RuntimePolicies.MaxRoomShows = legacy.RoomDisplayRules.MaxItems;
            config.RuntimePolicies.RoomCooldownTurns = legacy.RoomDisplayRules.CooldownTurns;
        }

        return config;
    }

    private static AdminAIStudioConfig NormalizeRequest(AdminAIStudioConfigRequest request)
    {
        var config = new AdminAIStudioConfig
        {
            Categories = request.Categories,
            AssistantProfile = request.AssistantProfile,
            ConversationFlows = request.ConversationFlows,
            FieldDefinitions = request.FieldDefinitions,
            UiBlockDefinitions = request.UiBlockDefinitions,
            RuntimePolicies = request.RuntimePolicies,
            Handoff = request.Handoff,
            BookingFormFields = request.BookingFormFields
        };

        if (request.ResponseStyle != null)
        {
            config.AssistantProfile.Tone = Choose(config.AssistantProfile.Tone, request.ResponseStyle.Tone);
            config.AssistantProfile.RolePrompt = Choose(config.AssistantProfile.RolePrompt, request.ResponseStyle.BasePrompt);
        }

        if (request.MemoryRules != null)
        {
            config.AssistantProfile.MissingInfoPrompt = Choose(config.AssistantProfile.MissingInfoPrompt, request.MemoryRules.MissingInfoRule);
        }

        if (request.SafetyRules != null)
        {
            config.AssistantProfile.SafetyPrompt = Choose(config.AssistantProfile.SafetyPrompt, request.SafetyRules.DataTruthRule);
        }

        if (request.RoomDisplayRules != null)
        {
            config.RuntimePolicies.AutoShowRooms = request.RoomDisplayRules.AutoShow;
            config.RuntimePolicies.MaxRoomShows = request.RoomDisplayRules.MaxItems;
            config.RuntimePolicies.RoomCooldownTurns = request.RoomDisplayRules.CooldownTurns;
        }

        EnsureStructuredShape(config);
        return config;
    }

    private static void EnsureStructuredShape(AdminAIStudioConfig config)
    {
        var defaults = BuildDefaultConfig();
        config.Categories = defaults.Categories;

        config.AssistantProfile ??= defaults.AssistantProfile;
        config.ConversationFlows ??= defaults.ConversationFlows;
        config.FieldDefinitions ??= defaults.FieldDefinitions;
        config.UiBlockDefinitions ??= defaults.UiBlockDefinitions;
        config.RuntimePolicies ??= defaults.RuntimePolicies;
        config.Handoff ??= defaults.Handoff;
        config.BookingFormFields ??= defaults.BookingFormFields;

        if (config.ConversationFlows.Count == 0)
        {
            config.ConversationFlows = defaults.ConversationFlows;
        }

        if (config.FieldDefinitions.Count == 0)
        {
            config.FieldDefinitions = defaults.FieldDefinitions;
        }

        if (config.UiBlockDefinitions.Count == 0)
        {
            config.UiBlockDefinitions = defaults.UiBlockDefinitions;
        }
    }

    private static bool HasProperty(JsonElement element, string name)
    {
        return element.EnumerateObject().Any(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetSetting(IReadOnlyDictionary<string, string> settings, string key, string fallback)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    private static bool GetBoolSetting(IReadOnlyDictionary<string, string> settings, string key, bool fallback)
    {
        return settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int GetIntSetting(IReadOnlyDictionary<string, string> settings, string key, int fallback)
    {
        return settings.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string Choose(string preferred, string fallback)
    {
        return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
    }

    private static string BuildPublicBookingPrompt(AdminAIStudioConfig request)
    {
        var flows = request.ConversationFlows
            .Where(flow => flow.Enabled)
            .OrderBy(flow => flow.Priority)
            .Select(flow => $"- {flow.Id}: {flow.Description}");

        return string.Join(Environment.NewLine,
            new[]
            {
                request.AssistantProfile?.RolePrompt ?? string.Empty,
                "Luôn dùng dữ liệu phòng/slot thật, không tự xác nhận booking.",
                "Flow đang bật:",
                string.Join(Environment.NewLine, flows)
            }.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static AdminAIStudioConfigResponse BuildDefaultConfig()
    {
        return new AdminAIStudioConfigResponse
        {
            Categories =
            [
                "Cấu hình Trợ lý & Handoff",
                "Luật phản hồi & Chốt",
                "Luồng đặt & Form khách"
            ],
            AssistantProfile = new AdminAIAssistantProfile
            {
                RolePrompt = "Bạn là AI sale-assist hỗ trợ khách tìm và đặt phòng.",
                Tone = "Thân thiện, rõ ràng, tư vấn như nhân viên sale.",
                MissingInfoPrompt = "Hỏi đúng thông tin còn thiếu và ưu tiên block nhập liệu phù hợp.",
                SafetyPrompt = "Không xác nhận booking, không thanh toán, không bịa dữ liệu."
            },
            ConversationFlows =
            [
                new AdminAIConversationFlow
                {
                    Id = "hourly",
                    Name = "Đặt theo giờ",
                    Description = "Thu thập chi nhánh, ngày, khung giờ, số khách và phòng phù hợp.",
                    Priority = 1,
                    RequiredFieldKeys = ["branchId", "hourlyDate", "hourlySlot", "guestCount"],
                    UiBlockTypes = ["branchSelector", "singleDatePicker", "timeSlotGrid", "roomCards", "bookingForm"]
                },
                new AdminAIConversationFlow
                {
                    Id = "daily",
                    Name = "Đặt qua đêm / theo ngày",
                    Description = "Thu thập chi nhánh, ngày nhận/trả, số khách và phòng phù hợp.",
                    Priority = 2,
                    RequiredFieldKeys = ["branchId", "checkInDate", "checkOutDate", "guestCount"],
                    UiBlockTypes = ["branchSelector", "dateRangePicker", "roomCards", "bookingForm"]
                }
            ],
            FieldDefinitions =
            [
                new AdminAIFieldDefinition
                {
                    Id = "field-branch",
                    Key = "branchId",
                    Label = "Chi nhánh",
                    InputType = "branchSelector",
                    SourceType = "branches",
                    QuestionTemplate = "Bạn muốn đặt ở chi nhánh nào ạ?",
                    FlowScopes = ["hourly", "daily"]
                },
                new AdminAIFieldDefinition
                {
                    Id = "field-hourly-date",
                    Key = "hourlyDate",
                    Label = "Ngày đặt theo giờ",
                    InputType = "date",
                    QuestionTemplate = "Bạn muốn đặt theo giờ vào ngày nào ạ?",
                    FlowScopes = ["hourly"]
                },
                new AdminAIFieldDefinition
                {
                    Id = "field-hourly-slot",
                    Key = "hourlySlot",
                    Label = "Khung giờ",
                    InputType = "timeSlot",
                    SourceType = "availability",
                    QuestionTemplate = "Bạn chọn khung giờ nào trong các slot còn trống ạ?",
                    FlowScopes = ["hourly"]
                },
                new AdminAIFieldDefinition
                {
                    Id = "field-check-in",
                    Key = "checkInDate",
                    Label = "Ngày nhận phòng",
                    InputType = "date",
                    QuestionTemplate = "Bạn muốn nhận phòng ngày nào ạ?",
                    FlowScopes = ["daily"]
                },
                new AdminAIFieldDefinition
                {
                    Id = "field-check-out",
                    Key = "checkOutDate",
                    Label = "Ngày trả phòng",
                    InputType = "date",
                    QuestionTemplate = "Bạn muốn trả phòng ngày nào ạ?",
                    FlowScopes = ["daily"]
                },
                new AdminAIFieldDefinition
                {
                    Id = "field-guest-count",
                    Key = "guestCount",
                    Label = "Số khách",
                    InputType = "number",
                    QuestionTemplate = "Nhóm mình có bao nhiêu khách ạ?",
                    FlowScopes = ["hourly", "daily"]
                }
            ],
            UiBlockDefinitions =
            [
                new AdminAIUiBlockDefinition
                {
                    Id = "block-booking-mode",
                    Type = "bookingModeChoice",
                    Label = "Chọn hình thức đặt",
                    Description = "Cho khách chọn đặt theo giờ hoặc qua đêm.",
                    FlowScopes = ["hourly", "daily"]
                },
                new AdminAIUiBlockDefinition
                {
                    Id = "block-branch",
                    Type = "branchSelector",
                    Label = "Chọn chi nhánh",
                    Description = "Hiển thị danh sách chi nhánh để khách chọn nhanh.",
                    FieldKeys = ["branchId"],
                    FlowScopes = ["hourly", "daily"]
                },
                new AdminAIUiBlockDefinition
                {
                    Id = "block-hourly-date",
                    Type = "singleDatePicker",
                    Label = "Chọn ngày",
                    Description = "Hiển thị lịch chọn ngày cho flow đặt theo giờ.",
                    FieldKeys = ["hourlyDate"],
                    FlowScopes = ["hourly"]
                },
                new AdminAIUiBlockDefinition
                {
                    Id = "block-date-range",
                    Type = "dateRangePicker",
                    Label = "Ngày nhận / trả",
                    Description = "Hiển thị lịch chọn ngày nhận và trả phòng.",
                    FieldKeys = ["checkInDate", "checkOutDate"],
                    FlowScopes = ["daily"]
                },
                new AdminAIUiBlockDefinition
                {
                    Id = "block-slots",
                    Type = "timeSlotGrid",
                    Label = "Khung giờ trống",
                    Description = "Hiển thị slot còn trống theo dữ liệu availability.",
                    SourceType = "availability",
                    FieldKeys = ["hourlySlot"],
                    FlowScopes = ["hourly"]
                },
                new AdminAIUiBlockDefinition
                {
                    Id = "block-rooms",
                    Type = "roomCards",
                    Label = "Phòng phù hợp",
                    Description = "Hiển thị danh sách phòng phù hợp từ dữ liệu thật.",
                    SourceType = "availability",
                    FlowScopes = ["hourly", "daily"]
                },
                new AdminAIUiBlockDefinition
                {
                    Id = "block-booking-form",
                    Type = "bookingForm",
                    Label = "Form đặt phòng",
                    Description = "Mở form đặt phòng chính thức khi đủ thông tin.",
                    FlowScopes = ["hourly", "daily"]
                },
                new AdminAIUiBlockDefinition
                {
                    Id = "block-booking-cta",
                    Type = "bookingCta",
                    Label = "Đi đến trang đặt phòng",
                    Description = "Hiển thị CTA chuyển khách sang luồng đặt phòng chính thức khi đã chọn phòng.",
                    FieldKeys = ["roomId"],
                    FlowScopes = ["hourly", "daily"]
                },
                new AdminAIUiBlockDefinition
                {
                    Id = "block-handoff",
                    Type = "handoffContact",
                    Label = "Handoff người thật",
                    Description = "Hiển thị hướng dẫn liên hệ nhân viên khi cần hỗ trợ.",
                    FlowScopes = ["hourly", "daily"]
                }
            ],
            RuntimePolicies = new AdminAIRuntimePolicies
            {
                RememberKnownCustomerInputs = true,
                AutoShowRooms = true,
                AutoShowSlots = true,
                MaxRoomShows = 6,
                RoomCooldownTurns = 3,
                MaxSlotShows = 8,
                SlotCooldownTurns = 2,
                DisplayRule = "Thu gọn theo quan hệ cha-con: branch -> room -> slot. Chỉ show block khi đã đủ parent field tương ứng và có dữ liệu thật từ hệ thống.",
                ClosingRule = "Vượt Capacity thì vẫn tư vấn và cảnh báo phụ thu; vượt MaxGuests thì loại khỏi gợi ý. Không tự xác nhận booking; luôn đưa khách sang luồng đặt phòng chính thức.",
                FallbackReplyRule = "Nếu khách nói tự nhiên nhưng chưa đủ field cha, chỉ hỏi đúng phần còn thiếu thay vì reset flow từ đầu.",
                SessionPersistenceRule = "Giữ ngữ cảnh phòng đang nói tới để các câu như 'phòng này ở 3 người được không' vẫn trả lời đúng."
            },
            Handoff = new AdminAIHandoffConfig
            {
                Enabled = true,
                TriggerPrompt = "Nếu khách cần hỗ trợ ngoài luồng tự động hoặc có vấn đề nhạy cảm, chuyển sang người thật.",
                ContactInstruction = "Mình sẽ nối bạn với chi nhánh phù hợp để được hỗ trợ trực tiếp.",
                Keywords = ["nhân viên", "gặp người", "khiếu nại", "hỗ trợ gấp"]
            },
            BookingFormFields = new List<BookingFormFieldConfig>
            {
                new() { Id = "customerName", Label = "Họ và tên", Type = "text", Enabled = true, Required = true, HelpText = "Nhập đúng họ tên người đặt phòng.", Order = 1 },
                new() { Id = "customerPhone", Label = "Số điện thoại", Type = "tel", Enabled = true, Required = true, HelpText = "Số điện thoại/Zalo để homestay liên hệ xác nhận.", Order = 2 },
                new() { Id = "customerEmail", Label = "Email", Type = "email", Enabled = true, Required = false, HelpText = "Email nhận thông tin đặt phòng nếu có.", Order = 3 },
                new() { Id = "customerNote", Label = "Ghi chú khách hàng", Type = "textarea", Enabled = false, Required = false, HelpText = "Các lưu ý đặc biệt khác.", Order = 4 }
            }
        };
    }

    private sealed class LegacyAdminAIStudioConfig
    {
        public AdminAIStudioResponseStyle? ResponseStyle { get; set; }
        public AdminAIStudioSafetyRules? SafetyRules { get; set; }
        public AdminAIStudioMemoryRules? MemoryRules { get; set; }
        public AdminAIStudioDisplayRules? RoomDisplayRules { get; set; }
    }
}
