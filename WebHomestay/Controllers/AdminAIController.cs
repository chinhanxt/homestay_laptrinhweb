using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Filters;
using WebHomestay.Models;
using WebHomestay.Models.AI;
using WebHomestay.Services.AI;

namespace WebHomestay.Controllers;

[AdminAuthorize]
[Route("admin/ai")]
public class AdminAIController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAdminAIStudioConfigService _studioConfigService;
    private readonly IAdminAIRuntimeSimulatorService _runtimeSimulatorService;
    private readonly IAdminAIReseedService _reseedService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IOperationalInsightService _operationalInsightService;

    public AdminAIController(
        ApplicationDbContext context,
        IAdminAIStudioConfigService studioConfigService,
        IAdminAIRuntimeSimulatorService runtimeSimulatorService,
        IAdminAIReseedService reseedService,
        IEmbeddingService embeddingService,
        IOperationalInsightService operationalInsightService)
    {
        _context = context;
        _studioConfigService = studioConfigService;
        _runtimeSimulatorService = runtimeSimulatorService;
        _reseedService = reseedService;
        _embeddingService = embeddingService;
        _operationalInsightService = operationalInsightService;
    }

    [AdminAuthorize(Permission = "ai.view")]
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [AdminAuthorize(Permission = "ai.response")]
    [HttpGet("studio-config")]
    public async Task<IActionResult> GetStudioConfig()
    {
        var config = await _studioConfigService.GetAsync();
        return Ok(config);
    }

    [AdminAuthorize(Permission = "ai.edit")]
    [HttpPost("studio-config")]
    public async Task<IActionResult> SaveStudioConfig([FromBody] AdminAIStudioConfigRequest request)
    {
        if (request == null)
        {
            return BadRequest("Studio config request is required.");
        }

        await _studioConfigService.SaveAsync(request);
        return Ok(new { success = true });
    }

    [AdminAuthorize(Permission = "ai.response")]
    [HttpGet("studio-simulator/presets")]
    public IActionResult GetStudioSimulatorPresets()
    {
        return Ok(_runtimeSimulatorService.GetPresets());
    }

    [AdminAuthorize(Permission = "ai.response")]
    [HttpPost("studio-simulator/run")]
    public async Task<IActionResult> RunStudioSimulator([FromBody] AdminAIRuntimeState request)
    {
        request ??= new AdminAIRuntimeState();
        var config = await _studioConfigService.GetAsync();
        var result = await _runtimeSimulatorService.SimulateAsync(config, request);
        return Ok(result);
    }

    [AdminAuthorize(Permission = "ai.edit")]
    [HttpPost("reseed-system-knowledge")]
    public async Task<IActionResult> ReseedSystemKnowledge()
    {
        var result = await _reseedService.ReseedAsync();
        return Ok(result);
    }

    [AdminAuthorize(Permission = "ai.knowledge")]
    [HttpGet("brain-knowledge")]
    public async Task<IActionResult> GetBrainKnowledge()
    {
        var scopes = await _context.AIBrainScopes
            .OrderBy(s => s.Order)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                s.IsActive,
                s.Order,
                Units = s.KnowledgeUnits
                    .Where(k => !k.IsDeleted)
                    .OrderByDescending(k => k.Priority)
                    .ThenByDescending(k => k.LastUpdated)
                    .Select(k => new
                    {
                        k.Id,
                        k.Title,
                        k.Content,
                        k.Tags,
                        k.Priority,
                        k.IsActive,
                        k.LastUpdated,
                        Source = k.Tags.Contains("system-seed") ? "system" : "manual"
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(scopes);
    }

    [AdminAuthorize(Permission = "ai.create")]
    [HttpPost("brain-scope")]
    public async Task<IActionResult> SaveBrainScope([FromBody] AIBrainScope scope)
    {
        if (scope.Id == Guid.Empty)
        {
            scope.Id = Guid.NewGuid();
        }

        var existing = await _context.AIBrainScopes.FindAsync(scope.Id);
        if (existing == null)
        {
            _context.AIBrainScopes.Add(scope);
        }
        else
        {
            existing.Name = scope.Name;
            existing.Description = scope.Description;
            existing.IsActive = scope.IsActive;
            existing.Order = scope.Order;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, id = scope.Id });
    }

    [AdminAuthorize(Permission = "ai.create")]
    [HttpPost("brain-knowledge-unit")]
    public async Task<IActionResult> SaveBrainKnowledgeUnit([FromBody] AIKnowledgeUnit unit)
    {
        if (unit.Id == Guid.Empty)
        {
            unit.Id = Guid.NewGuid();
        }

        var existing = await _context.AIKnowledgeUnits.FindAsync(unit.Id);
        if (existing == null)
        {
            unit.LastUpdated = DateTime.UtcNow;
            _context.AIKnowledgeUnits.Add(unit);
        }
        else
        {
            existing.ScopeId = unit.ScopeId;
            existing.Title = unit.Title;
            existing.Content = unit.Content;
            existing.Tags = unit.Tags;
            existing.Priority = unit.Priority;
            existing.IsActive = unit.IsActive;
            existing.LastUpdated = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, id = unit.Id });
    }

    [AdminAuthorize(Permission = "ai.delete")]
    [HttpDelete("brain-knowledge-unit/{id}")]
    public async Task<IActionResult> DeleteBrainKnowledgeUnit(Guid id)
    {
        var unit = await _context.AIKnowledgeUnits.FindAsync(id);
        if (unit == null) return NotFound();

        unit.IsDeleted = true;
        unit.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [AdminAuthorize(Permission = "ai.graph")]
    [HttpGet("brain-graph")]
    public async Task<IActionResult> GetBrainGraph()
    {
        var nodes = await _context.AIGraphNodes
            .Where(n => !n.IsDeleted)
            .OrderBy(n => n.NodeType)
            .ThenBy(n => n.Label)
            .Select(n => new
            {
                n.Id,
                n.NodeType,
                n.Label,
                n.Summary,
                n.MetadataJson,
                n.IsActive
            })
            .ToListAsync();

        var edges = await _context.AIGraphEdges
            .Where(e => !e.IsDeleted)
            .Include(e => e.FromNode)
            .Include(e => e.ToNode)
            .OrderBy(e => e.RelationshipType)
            .Select(e => new
            {
                e.Id,
                e.FromNodeId,
                FromLabel = e.FromNode.Label,
                e.ToNodeId,
                ToLabel = e.ToNode.Label,
                e.RelationshipType,
                e.Weight,
                e.Evidence
            })
            .ToListAsync();

        return Ok(new { nodes, edges });
    }

    [AdminAuthorize(Permission = "ai.create")]
    [HttpPost("brain-graph-node")]
    public async Task<IActionResult> SaveBrainGraphNode([FromBody] AIGraphNode node)
    {
        if (node.Id == Guid.Empty)
        {
            node.Id = Guid.NewGuid();
        }

        var existing = await _context.AIGraphNodes.FindAsync(node.Id);
        if (existing == null)
        {
            _context.AIGraphNodes.Add(node);
        }
        else
        {
            existing.NodeType = node.NodeType;
            existing.Label = node.Label;
            existing.Summary = node.Summary;
            existing.MetadataJson = string.IsNullOrWhiteSpace(node.MetadataJson) ? "{}" : node.MetadataJson;
            existing.IsActive = node.IsActive;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, id = node.Id });
    }

    [AdminAuthorize(Permission = "ai.delete")]
    [HttpDelete("brain-graph-node/{id}")]
    public async Task<IActionResult> DeleteBrainGraphNode(Guid id)
    {
        var node = await _context.AIGraphNodes.FindAsync(id);
        if (node == null) return NotFound();

        node.IsDeleted = true;
        node.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [AdminAuthorize(Permission = "ai.create")]
    [HttpPost("brain-graph-edge")]
    public async Task<IActionResult> SaveBrainGraphEdge([FromBody] AIGraphEdge edge)
    {
        if (edge.Id == Guid.Empty)
        {
            edge.Id = Guid.NewGuid();
        }

        var existing = await _context.AIGraphEdges.FindAsync(edge.Id);
        if (existing == null)
        {
            _context.AIGraphEdges.Add(edge);
        }
        else
        {
            existing.FromNodeId = edge.FromNodeId;
            existing.ToNodeId = edge.ToNodeId;
            existing.RelationshipType = edge.RelationshipType;
            existing.Weight = edge.Weight;
            existing.Evidence = edge.Evidence;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, id = edge.Id });
    }

    [AdminAuthorize(Permission = "ai.delete")]
    [HttpDelete("brain-graph-edge/{id}")]
    public async Task<IActionResult> DeleteBrainGraphEdge(Guid id)
    {
        var edge = await _context.AIGraphEdges.FindAsync(id);
        if (edge == null) return NotFound();

        edge.IsDeleted = true;
        edge.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [AdminAuthorize(Permission = "ai.delete")]
    [HttpGet("brain-trash")]
    public async Task<IActionResult> GetBrainTrash()
    {
        var units = await _context.AIKnowledgeUnits
            .Include(u => u.Scope)
            .Where(u => u.IsDeleted)
            .OrderByDescending(u => u.DeletedAt)
            .Select(u => new
            {
                u.Id,
                u.Title,
                u.Content,
                u.Tags,
                u.IsActive,
                u.DeletedAt,
                Type = "knowledge",
                ScopeName = u.Scope.Name
            })
            .ToListAsync();

        var nodes = await _context.AIGraphNodes
            .Where(n => n.IsDeleted)
            .OrderByDescending(n => n.DeletedAt)
            .Select(n => new
            {
                n.Id,
                n.Label,
                n.Summary,
                n.NodeType,
                n.DeletedAt,
                Type = "node"
            })
            .ToListAsync();

        var edges = await _context.AIGraphEdges
            .Include(e => e.FromNode)
            .Include(e => e.ToNode)
            .Where(e => e.IsDeleted)
            .OrderByDescending(e => e.DeletedAt)
            .Select(e => new
            {
                e.Id,
                e.RelationshipType,
                FromLabel = e.FromNode.Label,
                ToLabel = e.ToNode.Label,
                e.DeletedAt,
                Type = "edge"
            })
            .ToListAsync();

        return Ok(new { units, nodes, edges });
    }

    [AdminAuthorize(Permission = "ai.delete")]
    [HttpPost("brain-restore")]
    public async Task<IActionResult> RestoreBrainItem([FromBody] RestoreRequest request)
    {
        switch (request.Type)
        {
            case "knowledge":
                var unit = await _context.AIKnowledgeUnits.FindAsync(request.Id);
                if (unit == null) return NotFound();
                unit.IsDeleted = false;
                unit.DeletedAt = null;
                break;
            case "node":
                var node = await _context.AIGraphNodes.FindAsync(request.Id);
                if (node == null) return NotFound();
                node.IsDeleted = false;
                node.DeletedAt = null;
                break;
            case "edge":
                var edge = await _context.AIGraphEdges.FindAsync(request.Id);
                if (edge == null) return NotFound();
                edge.IsDeleted = false;
                edge.DeletedAt = null;
                break;
            default:
                return BadRequest(new { success = false, message = "Invalid type" });
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [AdminAuthorize(Permission = "ai.delete")]
    [HttpPost("brain-permanent-delete")]
    public async Task<IActionResult> PermanentDeleteBrainItem([FromBody] RestoreRequest request)
    {
        switch (request.Type)
        {
            case "knowledge":
                var unit = await _context.AIKnowledgeUnits.FindAsync(request.Id);
                if (unit == null) return NotFound();
                _context.AIKnowledgeUnits.Remove(unit);
                break;
            case "node":
                var node = await _context.AIGraphNodes.FindAsync(request.Id);
                if (node == null) return NotFound();
                var edges = await _context.AIGraphEdges
                    .Where(e => e.FromNodeId == request.Id || e.ToNodeId == request.Id)
                    .ToListAsync();
                _context.AIGraphEdges.RemoveRange(edges);
                _context.AIGraphNodes.Remove(node);
                break;
            case "edge":
                var edge = await _context.AIGraphEdges.FindAsync(request.Id);
                if (edge == null) return NotFound();
                _context.AIGraphEdges.Remove(edge);
                break;
            default:
                return BadRequest(new { success = false, message = "Invalid type" });
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    public class RestoreRequest
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    [AdminAuthorize(Permission = "ai.edit")]
    [HttpPost("reindex-embeddings")]
    public async Task<IActionResult> ReindexEmbeddings(CancellationToken cancellationToken = default)
    {
        try
        {
            var units = await _context.AIKnowledgeUnits
                .Where(k => k.IsActive && !k.IsDeleted)
                .ToListAsync(cancellationToken);

            int count = 0;
            foreach (var unit in units)
            {
                var text = $"{unit.Title}. {unit.Content}. Thẻ: {unit.Tags}";
                unit.Embedding = await _embeddingService.GetEmbeddingAsync(text, cancellationToken);
                count++;
            }

            var rooms = await _context.Rooms
                .Include(r => r.Branch)
                .ToListAsync(cancellationToken);

            foreach (var room in rooms)
            {
                var branchName = room.Branch?.Name ?? "Hệ thống";
                var text = $"Phòng {room.Name} thuộc chi nhánh {branchName}, giá giờ {room.PricePerHour:N0}đ, giá ngày {room.PricePerDay:N0}đ. Sức chứa {room.Capacity} người, tối đa {room.MaxGuests} khách. Tiện nghi và mô tả: {room.Description ?? "Chưa cập nhật."}";
                room.Embedding = await _embeddingService.GetEmbeddingAsync(text, cancellationToken);
                count++;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, message = $"Đồng bộ thành công {count} bản ghi vector embedding." });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"Lỗi đồng bộ vector: {ex.Message}" });
        }
    }

    [AdminAuthorize(Permission = "ai.view")]
    [HttpGet("operational-briefing")]
    public async Task<IActionResult> GetOperationalBriefing(CancellationToken cancellationToken = default)
    {
        var briefing = await _operationalInsightService.GetDailyBriefingJsonAsync(cancellationToken);
        return Content(briefing, "application/json");
    }

    [AdminAuthorize(Permission = "ai.edit")]
    [HttpPost("execute-quick-action")]
    public async Task<IActionResult> ExecuteQuickAction([FromBody] QuickActionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Action))
        {
            return BadRequest("Yêu cầu không hợp lệ.");
        }

        try
        {
            string redirectUrl = "";
            string message = "Hành động đã được xử lý.";

            switch (request.Action.ToUpperInvariant())
            {
                case "VIEW_BOOKINGS":
                    redirectUrl = "/chinhan/hethong/bookings";
                    message = "Đang chuyển hướng đến danh sách đặt phòng.";
                    break;
                case "VIEW_CANCELLATIONS":
                    redirectUrl = "/chinhan/hethong/chat-monitor";
                    message = "Đang chuyển hướng đến trang duyệt yêu cầu hủy phòng.";
                    break;
                case "VIEW_CHATS":
                    redirectUrl = "/chinhan/hethong/chat-monitor";
                    message = "Đang chuyển hướng đến màn hình điều phối chat.";
                    break;
                case "VIEW_MAINTENANCE":
                    redirectUrl = "/chinhan/hethong/rooms";
                    message = "Đang chuyển hướng đến quản lý phòng.";
                    break;
                case "CLEAN_ROOM":
                    var roomName = request.Param;
                    if (!string.IsNullOrWhiteSpace(roomName))
                    {
                        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Name == roomName, cancellationToken);
                        if (room != null)
                        {
                            room.Status = "Available";
                            await _context.SaveChangesAsync(cancellationToken);
                            message = $"Đã chuyển trạng thái phòng {roomName} sang Sẵn sàng hoạt động.";
                        }
                    }
                    redirectUrl = "/chinhan/hethong/rooms";
                    break;
                default:
                    message = $"Không tìm thấy handler cho hành động {request.Action}";
                    break;
            }

            return Ok(new { success = true, message, redirectUrl });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"Lỗi thực thi hành động: {ex.Message}" });
        }
    }
}

public class QuickActionRequest
{
    public string Action { get; set; } = string.Empty;
    public string Param { get; set; } = string.Empty;
}
