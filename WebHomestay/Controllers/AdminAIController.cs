using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Controllers
{
    [Route("admin/ai")]
    public class AdminAIController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminAIController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("collections")]
        public async Task<IActionResult> GetCollections()
        {
            var collections = await _context.AIKnowledgeCollections
                .OrderBy(c => c.Order)
                .ToListAsync();

            if (!collections.Any())
            {
                // Seed default collections if none exist
                collections = new List<AIKnowledgeCollection>
                {
                    new AIKnowledgeCollection { Name = "Kỹ năng Sales & Thuyết phục", Icon = "fa-comments-dollar", Order = 1, Description = "Các kịch bản chốt đơn và xử lý từ chối." },
                    new AIKnowledgeCollection { Name = "Kiến thức Hệ thống", Icon = "fa-database", Order = 2, Description = "Thông tin chi tiết về chi nhánh và phòng." },
                    new AIKnowledgeCollection { Name = "Quy định & Chính sách", Icon = "fa-file-contract", Order = 3, Description = "Nội quy và chính sách đặt/hủy phòng." }
                };
                _context.AIKnowledgeCollections.AddRange(collections);
                await _context.SaveChangesAsync();
            }

            return Ok(collections);
        }

        [HttpGet("articles/{collectionId}")]
        public async Task<IActionResult> GetArticles(Guid collectionId)
        {
            var articles = await _context.AIKnowledgeArticles
                .Where(a => a.CollectionId == collectionId)
                .OrderByDescending(a => a.LastUpdated)
                .Select(a => new {
                    a.Id,
                    a.Title,
                    a.LastUpdated,
                    ContentPreview = a.Content != null && a.Content.Length > 100 ? a.Content.Substring(0, 100) + "..." : a.Content
                })
                .ToListAsync();

            return Ok(articles);
        }

        [HttpGet("article/{id}")]
        public async Task<IActionResult> GetArticle(Guid id)
        {
            var article = await _context.AIKnowledgeArticles.FindAsync(id);
            if (article == null) return NotFound();
            return Ok(article);
        }

        [HttpPost("save-article")]
        public async Task<IActionResult> SaveArticle([FromBody] AIKnowledgeArticle article)
        {
            if (article.Id == Guid.Empty) article.Id = Guid.NewGuid();

            var existing = await _context.AIKnowledgeArticles.FindAsync(article.Id);
            if (existing == null)
            {
                article.LastUpdated = DateTime.Now;
                _context.AIKnowledgeArticles.Add(article);
            }
            else
            {
                existing.Title = article.Title;
                existing.Content = article.Content;
                existing.CollectionId = article.CollectionId;
                existing.LastUpdated = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = article.Id });
        }

        [HttpDelete("article/{id}")]
        public async Task<IActionResult> DeleteArticle(Guid id)
        {
            var article = await _context.AIKnowledgeArticles.FindAsync(id);
            if (article == null) return NotFound();

            _context.AIKnowledgeArticles.Remove(article);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
