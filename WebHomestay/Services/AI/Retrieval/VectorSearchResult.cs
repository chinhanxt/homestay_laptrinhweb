using System;

namespace WebHomestay.Services.AI.Retrieval
{
    public sealed class VectorSearchResult
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public double Score { get; set; }
    }

    public sealed class VectorSearchFilter
    {
        public Guid? ScopeId { get; set; }
        public int? BranchId { get; set; }
        public int? RoomId { get; set; }
        public bool ActiveOnly { get; set; } = true;
    }
}
