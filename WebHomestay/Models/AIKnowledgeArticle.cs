using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models
{
    public class AIKnowledgeArticle
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CollectionId { get; set; }
        public AIKnowledgeCollection Collection { get; set; }
        [Required]
        public string Title { get; set; }
        public string Content { get; set; } // Markdown/HTML
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
