using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models
{
    public class AIKnowledgeCollection
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string Name { get; set; }
        public string Icon { get; set; } = "fa-book";
        public string Description { get; set; }
        public int Order { get; set; }
        public ICollection<AIKnowledgeArticle> Articles { get; set; }
    }
}
