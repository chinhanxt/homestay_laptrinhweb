using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Enums;
using System.Collections.Generic;

namespace WebHomestay.Models.DTOs.AI;

public class ScoredRoom
{
    public Room Room { get; set; } = null!;
    public decimal RelevanceScore { get; set; }
    public Dictionary<string, decimal> ScoreBreakdown { get; set; } = new();
    public string? ExplanationForUser { get; set; }
}
