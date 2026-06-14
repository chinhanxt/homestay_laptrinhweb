using System.Collections.Generic;
using WebHomestay.Models;

namespace WebHomestay.Models.AI;

public class ScoredRoom
{
    public Room Room { get; set; } = null!;
    public decimal RelevanceScore { get; set; }
    public Dictionary<string, decimal> ScoreBreakdown { get; set; } = new();
    public string? ExplanationForUser { get; set; }
}
