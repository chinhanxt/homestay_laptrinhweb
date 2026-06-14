using System.Text.Json.Serialization;

namespace WebHomestay.Models.AI;

public class AdminAIStudioBlock
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SubmitBehavior { get; set; } = "compose_and_send";
    public string MessageTemplate { get; set; } = string.Empty;
    public string LayoutMode { get; set; } = "inline";
    public string Source { get; set; } = "system";
    public List<string> FieldKeys { get; set; } = new();
    public List<AdminAIStudioCondition> VisibilityRules { get; set; } = new();
    public string StyleVariant { get; set; } = "default";

    [JsonIgnore]
    public string DisplayMode
    {
        get => LayoutMode;
        set => LayoutMode = value;
    }

    [JsonIgnore]
    public List<AdminAIStudioField> Fields { get; set; } = new();

    [JsonIgnore]
    public List<AdminAIStudioCondition> TriggerConditions
    {
        get => VisibilityRules;
        set => VisibilityRules = value;
    }
}

public class AdminAIUiBlockDefinition : AdminAIStudioBlock
{
    public string SourceType
    {
        get => Source;
        set => Source = value;
    }

    public List<string> FlowScopes { get; set; } = new();

    public List<AdminAIStudioCondition> TriggerRules
    {
        get => VisibilityRules;
        set => VisibilityRules = value;
    }
}
