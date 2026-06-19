using System.Text.Json.Serialization;

namespace WebHomestay.Models.Configuration;

public class AdminAIStudioField
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string InputType { get; set; } = string.Empty;
    public string SourceType { get; set; } = "manual";
    public bool IsSystem { get; set; } = true;
    public bool IsRequired { get; set; } = true;
    public string CaptureMode { get; set; } = "single-question";
    public string QuestionTemplate { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<string> FlowScopes { get; set; } = new();
    public List<AdminAIStudioCondition> TriggerRules { get; set; } = new();
    public List<string> ValidationRules { get; set; } = new();
    public List<AdminAIStudioFieldOption> Options { get; set; } = new();

    [JsonIgnore]
    public string Type
    {
        get => InputType;
        set => InputType = value;
    }

    [JsonIgnore]
    public bool Required
    {
        get => IsRequired;
        set => IsRequired = value;
    }

    [JsonIgnore]
    public int Order
    {
        get => SortOrder;
        set => SortOrder = value;
    }

    [JsonIgnore]
    public string Source
    {
        get => SourceType;
        set => SourceType = value;
    }
}

public class AdminAIFieldDefinition : AdminAIStudioField
{
}

public class AdminAIStudioFieldOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
