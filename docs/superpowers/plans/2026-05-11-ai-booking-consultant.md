# AI Booking Consultant Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the "Knowledge Hub" (Digital Academy) for the AI Booking Consultant, allowing Admins to train the AI through categorized Markdown articles and test responses in a playground.

**Architecture:** ASP.NET Core MVC with Entity Framework Core. AJAX-driven UI using modern CSS (Bento Grid) and a custom Markdown editor.

**Tech Stack:** C#, .NET 8, Entity Framework Core, JavaScript (Vanilla/jQuery), CSS3, FontAwesome.

---

### Task 1: Data Infrastructure

**Files:**
- Create: `WebHomestay/Models/AIKnowledgeCollection.cs`
- Create: `WebHomestay/Models/AIKnowledgeArticle.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Create AIKnowledgeCollection model**
```csharp
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
```

- [ ] **Step 2: Create AIKnowledgeArticle model**
```csharp
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
```

- [ ] **Step 3: Update ApplicationDbContext**
Add the following lines to `ApplicationDbContext.cs`:
```csharp
public DbSet<AIKnowledgeCollection> AIKnowledgeCollections { get; set; }
public DbSet<AIKnowledgeArticle> AIKnowledgeArticles { get; set; }
```

- [ ] **Step 4: Commit**
```bash
git add WebHomestay/Models/AIKnowledgeCollection.cs WebHomestay/Models/AIKnowledgeArticle.cs WebHomestay/Data/ApplicationDbContext.cs
git commit -m "feat: add AI Knowledge Hub data models"
```

---

### Task 2: Backend API for Knowledge Management

**Files:**
- Create: `WebHomestay/Controllers/AdminAIController.cs`

- [ ] **Step 1: Create AdminAIController with CRUD actions**
Implement basic JSON endpoints for fetching collections, articles, and saving content.
```csharp
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
        public AdminAIController(ApplicationDbContext context) => _context = context;

        [HttpGet("collections")]
        public async Task<IActionResult> GetCollections() 
            => Ok(await _context.AIKnowledgeCollections.OrderBy(c => c.Order).ToListAsync());

        [HttpGet("articles/{collectionId}")]
        public async Task<IActionResult> GetArticles(Guid collectionId)
            => Ok(await _context.AIKnowledgeArticles.Where(a => a.CollectionId == collectionId).ToListAsync());

        [HttpPost("save-article")]
        public async Task<IActionResult> SaveArticle([FromBody] AIKnowledgeArticle article)
        {
            var existing = await _context.AIKnowledgeArticles.FindAsync(article.Id);
            if (existing == null) _context.AIKnowledgeArticles.Add(article);
            else {
                existing.Title = article.Title;
                existing.Content = article.Content;
                existing.LastUpdated = DateTime.Now;
            }
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
```

- [ ] **Step 2: Commit**
```bash
git add WebHomestay/Controllers/AdminAIController.cs
git commit -m "feat: add AdminAIController for knowledge management"
```

---

### Task 3: UI - Knowledge Hub Sidebar & Article List

**Files:**
- Modify: `WebHomestay/Views/AdminSettings/Index.cshtml`
- Create: `WebHomestay/wwwroot/css/admin-ai-hub.css`

- [ ] **Step 1: Implement the Bento-style layout for the AI tab**
Update the `#panel-ai` container in `Index.cshtml` to include a 3-column layout.
```html
<div class="ai-hub-container">
    <div class="ai-sidebar">
        <h6 class="fw-bold mb-3 px-3">KHO TRI THỨC</h6>
        <div id="ai-collections-list" class="nav flex-column">
            <!-- Loaded via AJAX -->
        </div>
    </div>
    <div class="ai-main-content">
        <div id="ai-articles-view">
            <div class="panel-header d-flex justify-content-between align-items-center">
                <h4 id="current-collection-name">Chọn danh mục...</h4>
                <button class="btn-premium btn-sm" onclick="createNewArticle()"><i class="fas fa-plus"></i></button>
            </div>
            <div id="ai-articles-list" class="articles-grid"></div>
        </div>
    </div>
    <div class="ai-right-panel">
        <div class="bento-item mb-4">
            <div class="bento-inner">
                <h6 class="fw-bold"><i class="fas fa-rss text-success me-2"></i>Live Data Feed</h6>
                <ul class="data-feed-list small">
                    <li><i class="fas fa-check-circle text-success"></i> Chi nhánh (Live)</li>
                    <li><i class="fas fa-check-circle text-success"></i> Trạng thái phòng (Live)</li>
                </ul>
            </div>
        </div>
        <!-- Testing Playground Placeholder -->
    </div>
</div>
```

- [ ] **Step 2: Add CSS for the AI Hub**
```css
.ai-hub-container { display: grid; grid-template-columns: 240px 1fr 300px; gap: 1.5rem; height: 700px; }
.ai-sidebar { background: white; border-radius: 20px; padding: 1.5rem 0; border: 1px solid #f1f5f9; }
.ai-main-content { overflow-y: auto; }
.ai-right-panel { display: flex; flex-direction: column; }
.data-feed-list { list-style: none; padding: 0; margin-top: 1rem; }
.data-feed-list li { margin-bottom: 0.5rem; display: flex; align-items: center; gap: 10px; }
```

- [ ] **Step 3: Commit**
```bash
git add WebHomestay/Views/AdminSettings/Index.cshtml WebHomestay/wwwroot/css/admin-ai-hub.css
git commit -m "feat: implement AI Hub UI layout"
```

---

### Task 4: Markdown Editor & Article Editor

**Files:**
- Create: `WebHomestay/wwwroot/js/admin-ai-hub.js`

- [ ] **Step 1: Implement Article Editing Logic**
Add JS to handle article selection and opening a modal editor with Markdown preview.
```javascript
function openArticleEditor(articleId) {
    // Fetch article content
    // Open modal with textarea and preview div
}

function saveArticle() {
    const data = {
        Id: $('#edit-id').val(),
        Title: $('#edit-title').val(),
        Content: $('#edit-content').val(),
        CollectionId: currentCollectionId
    };
    fetch('/admin/ai/save-article', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    }).then(res => res.json()).then(data => {
        if(data.success) {
            refreshArticles();
            $('#articleEditorModal').modal('hide');
        }
    });
}
```

- [ ] **Step 2: Commit**
```bash
git add WebHomestay/wwwroot/js/admin-ai-hub.js
git commit -m "feat: add article editing and saving logic"
```

---

### Task 5: Testing Playground (Interactive Chat)

**Files:**
- Modify: `WebHomestay/Views/AdminSettings/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-hub.js`

- [ ] **Step 1: Add Chat UI to Right Panel**
```html
<div class="bento-item flex-grow-1">
    <div class="bento-inner d-flex flex-direction-column h-100">
        <h6 class="fw-bold mb-3"><i class="fas fa-vial text-primary me-2"></i>Testing Playground</h6>
        <div id="ai-chat-history" class="chat-history mb-3"></div>
        <div class="chat-input-wrapper">
            <input type="text" id="ai-test-input" placeholder="Hỏi AI để test kiến thức..." class="form-control-premium">
            <button onclick="sendTestMessage()" class="btn-send"><i class="fas fa-paper-plane"></i></button>
        </div>
    </div>
</div>
```

- [ ] **Step 2: Mock AI Response Logic**
```javascript
function sendTestMessage() {
    const msg = $('#ai-test-input').val();
    if(!msg) return;
    $('#ai-chat-history').append(`<div class="msg user">${msg}</div>`);
    $('#ai-test-input').val('');
    
    // Mock response for now
    setTimeout(() => {
        $('#ai-chat-history').append(`<div class="msg ai">Chào Admin, tôi đã nhận được thông tin. Tôi sẽ trả lời dựa trên những gì bạn đã dạy tôi trong bài học Markdown!</div>`);
    }, 800);
}
```

- [ ] **Step 3: Commit**
```bash
git add WebHomestay/Views/AdminSettings/Index.cshtml WebHomestay/wwwroot/js/admin-ai-hub.js
git commit -m "feat: add AI Testing Playground UI and mock logic"
```
