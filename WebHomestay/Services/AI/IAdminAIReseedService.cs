namespace WebHomestay.Services.AI;

public interface IAdminAIReseedService
{
    Task<AdminAIReseedResult> ReseedAsync();
}

public class AdminAIReseedResult
{
    public int CreatedScopes { get; set; }
    public int CreatedKnowledgeUnits { get; set; }
    public int CreatedNodes { get; set; }
    public int CreatedEdges { get; set; }
    public int TotalScopes { get; set; }
    public int TotalKnowledgeUnits { get; set; }
    public int TotalNodes { get; set; }
    public int TotalEdges { get; set; }
}
