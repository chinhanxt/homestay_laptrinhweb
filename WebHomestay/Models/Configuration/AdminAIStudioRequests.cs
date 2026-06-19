using System.Text.Json.Serialization;

namespace WebHomestay.Models.Configuration;

public class AdminAIStudioConfigRequest : AdminAIStudioConfig
{
    [JsonIgnore]
    public new AdminAIStudioResponseStyle? ResponseStyle { get; set; }

    [JsonIgnore]
    public AdminAIStudioSafetyRules? SafetyRules { get; set; }

    [JsonIgnore]
    public AdminAIStudioMemoryRules? MemoryRules { get; set; }

    [JsonIgnore]
    public AdminAIStudioDisplayRules? RoomDisplayRules { get; set; }
}

public class AdminAIStudioConfigResponse : AdminAIStudioConfig
{
    public DateTime? LastUpdated { get; set; }
}
