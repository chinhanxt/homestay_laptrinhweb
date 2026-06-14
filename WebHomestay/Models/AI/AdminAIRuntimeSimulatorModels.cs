namespace WebHomestay.Models.AI;

public class AdminAIRuntimeState
{
    public string FlowId { get; set; } = string.Empty;
    public string LastCustomerMessage { get; set; } = string.Empty;
    public Dictionary<string, string> CapturedFields { get; set; } = new();
    public bool NeedHumanHandoff { get; set; }
}

public class AdminAIRuntimeConversationState
{
    public string FlowId { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public Dictionary<string, string> CapturedFields { get; set; } = new();
    public List<string> MissingFields { get; set; } = new();
    public bool NeedHumanHandoff { get; set; }
}

public class AdminAIRuntimeSimulationResult
{
    public AdminAIRuntimeConversationState ConversationState { get; set; } = new();
    public string AssistantReply { get; set; } = string.Empty;
    public List<AdminAIUiDirective> UiDirectives { get; set; } = new();
    public List<string> NextActions { get; set; } = new();
}

public class AdminAIUiDirective
{
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public List<string> FieldKeys { get; set; } = new();
    public Dictionary<string, string> Data { get; set; } = new();
}

public class AdminAIRuntimePreset
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AdminAIRuntimeState State { get; set; } = new();
}

public static class AdminAIRuntimePresetCatalog
{
    public static IReadOnlyList<AdminAIRuntimePreset> All { get; } =
    [
        new()
        {
            Id = "new-chat",
            Label = "Khach moi mo chat",
            Description = "Chua co flow, can hien lua chon dat theo gio/ngay.",
            State = new AdminAIRuntimeState()
        },
        new()
        {
            Id = "hourly-missing-branch",
            Label = "Theo gio thieu chi nhanh",
            Description = "Da vao flow theo gio nhung chua co branch.",
            State = new AdminAIRuntimeState { FlowId = "hourly" }
        },
        new()
        {
            Id = "hourly-missing-date",
            Label = "Theo gio thieu ngay",
            Description = "Da co branch, can hoi ngay dat theo gio.",
            State = new AdminAIRuntimeState
            {
                FlowId = "hourly",
                CapturedFields = new Dictionary<string, string> { ["branchId"] = "1" }
            }
        },
        new()
        {
            Id = "hourly-ready-for-slots",
            Label = "Du dieu kien show gio trong",
            Description = "Da co branch va ngay, can hien slot trong.",
            State = new AdminAIRuntimeState
            {
                FlowId = "hourly",
                CapturedFields = new Dictionary<string, string>
                {
                    ["branchId"] = "1",
                    ["hourlyDate"] = "2026-06-10"
                }
            }
        },
        new()
        {
            Id = "daily-missing-range",
            Label = "Theo ngay thieu in-out",
            Description = "Da vao flow theo ngay va can form check-in/check-out.",
            State = new AdminAIRuntimeState
            {
                FlowId = "daily",
                CapturedFields = new Dictionary<string, string> { ["branchId"] = "1" }
            }
        },
        new()
        {
            Id = "ready-for-room-cards",
            Label = "Du dieu kien show phong",
            Description = "Da co du thong tin chinh, can hien phong phu hop.",
            State = new AdminAIRuntimeState
            {
                FlowId = "daily",
                CapturedFields = new Dictionary<string, string>
                {
                    ["branchId"] = "1",
                    ["checkInDate"] = "2026-06-10",
                    ["checkOutDate"] = "2026-06-12",
                    ["guestCount"] = "2"
                }
            }
        },
        new()
        {
            Id = "handoff",
            Label = "Khach can nguoi that",
            Description = "Kich hoat handoff, can xin so dien thoai va hien lien he chi nhanh.",
            State = new AdminAIRuntimeState { NeedHumanHandoff = true }
        }
    ];
}
