using System.Text.Json.Serialization;

namespace WebHomestay.Models.Configuration;

public class AdminAIStudioConfig
{
    public List<string> Categories { get; set; } = new();
    public AdminAIAssistantProfile AssistantProfile { get; set; } = new();
    public List<AdminAIConversationFlow> ConversationFlows { get; set; } = new();
    public List<AdminAIFieldDefinition> FieldDefinitions { get; set; } = new();
    public List<AdminAIUiBlockDefinition> UiBlockDefinitions { get; set; } = new();
    public AdminAIRuntimePolicies RuntimePolicies { get; set; } = new();
    public AdminAIHandoffConfig Handoff { get; set; } = new();
    public List<BookingFormFieldConfig> BookingFormFields { get; set; } = new();

    // Temporary compatibility shim while controller/UI/tests are migrated in later tasks.
    [JsonIgnore]
    public AdminAIStudioResponseStyle ResponseStyle
    {
        get => new()
        {
            Tone = AssistantProfile.Tone,
            BasePrompt = AssistantProfile.RolePrompt,
            LanguageRule = AssistantProfile.LanguageRule
        };
        set
        {
            if (value == null)
            {
                return;
            }

            AssistantProfile.Tone = value.Tone;
            AssistantProfile.RolePrompt = value.BasePrompt;
            AssistantProfile.LanguageRule = value.LanguageRule;
        }
    }
}

public class AdminAIAssistantProfile
{
    public string RolePrompt { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public string LanguageRule { get; set; } = string.Empty;
    public string MissingInfoPrompt { get; set; } = string.Empty;
    public string SafetyPrompt { get; set; } = string.Empty;
    public string DataTruthRule { get; set; } = string.Empty;
    public string BookingRule { get; set; } = string.Empty;
}

public class AdminAIConversationFlow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public List<string> RequiredFieldKeys { get; set; } = new();
    public List<string> UiBlockTypes { get; set; } = new();
    public List<AdminAIStudioCondition> TriggerRules { get; set; } = new();
    public List<AdminAIStudioCondition> EntryConditions { get; set; } = new();
    public List<string> PriorityFields { get; set; } = new();
    public List<string> NextActionRules { get; set; } = new();
    public List<string> CompletionRules { get; set; } = new();
}

public class AdminAIRuntimePolicies
{
    public bool RememberKnownCustomerInputs { get; set; } = true;
    public bool AutoShowRooms { get; set; } = true;
    public bool AutoShowSlots { get; set; } = true;
    public int MaxRoomShows { get; set; } = 6;
    public int RoomCooldownTurns { get; set; } = 3;
    public int MaxSlotShows { get; set; } = 8;
    public int SlotCooldownTurns { get; set; } = 2;
    public string DisplayRule { get; set; } = string.Empty;
    public string ClosingRule { get; set; } = string.Empty;
    public AdminAIStudioDisplayRules ShowRoomRules { get; set; } = new();
    public AdminAIStudioDisplayRules ShowSlotRules { get; set; } = new();
    public AdminAIStudioDisplayRules ShowBookingCtaRules { get; set; } = new();
    public string FallbackReplyRule { get; set; } = string.Empty;
    public string SessionPersistenceRule { get; set; } = string.Empty;
    public string TriggerWords { get; set; } = "đặt,chốt,lấy,book,giữ phòng,giữ chỗ";
    public string ExitKeywords { get; set; } = "thôi,bỏ,khác,xóa,hủy,không,để sau";
    public string ProactiveMode { get; set; } = "balanced";
    public string DependencyRule { get; set; } = "branch->room->slot";
    public string GuestOverflowRule { get; set; } = "capacity_warn_max_filter";
}

public class AdminAIHandoffConfig
{
    public bool Enabled { get; set; } = true;
    public string TriggerPrompt { get; set; } = string.Empty;
    public string ContactInstruction { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public string MessageTemplate { get; set; } = string.Empty;
    public bool RequireCustomerPhone { get; set; } = true;
    public List<string> CoreTriggers { get; set; } = new();
    public List<string> CustomTriggers { get; set; } = new();
    public string KnownBranchRule { get; set; } = string.Empty;
    public string UnknownBranchRule { get; set; } = string.Empty;
}

public class AdminAIStudioResponseStyle
{
    public string Tone { get; set; } = string.Empty;
    public string BasePrompt { get; set; } = string.Empty;
    public string LanguageRule { get; set; } = string.Empty;
}

public class AdminAIStudioSafetyRules
{
    public string DataTruthRule { get; set; } = string.Empty;
    public string BookingRule { get; set; } = string.Empty;
    public string FormRule { get; set; } = string.Empty;
}

public class AdminAIStudioMemoryRules
{
    public bool RememberKnownCustomerInputs { get; set; } = true;
    public string MemoryRule { get; set; } = string.Empty;
    public string MissingInfoRule { get; set; } = string.Empty;
}

public class AdminAIStudioDisplayRules
{
    public bool AutoShow { get; set; }
    public int MaxItems { get; set; }
    public int CooldownTurns { get; set; }
    public string DisplayRule { get; set; } = string.Empty;
}

public class BookingFormFieldConfig
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool Required { get; set; } = true;
    public string HelpText { get; set; } = string.Empty;
    public int Order { get; set; }
}
