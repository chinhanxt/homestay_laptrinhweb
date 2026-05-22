using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using WebHomestay.Filters;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/ai")]
    public class AdminAIController : Controller
    {
        private const string DefaultFinalStyle = "Giọng thân thiện, rõ ràng, tư vấn như lễ tân chuyên nghiệp. Trả lời ngắn gọn nhưng đủ ý. Nếu thiếu thông tin thì hỏi lại bằng các câu hỏi cụ thể.";
        private const string DefaultBasePrompt = "Bạn là Final Response Synthesizer của AI Brain Center cho homestay self check-in/self check-out. Nhiệm vụ duy nhất: viết câu trả lời cuối cùng cho khách dựa trên dữ liệu các agent cung cấp.";
        private const string DefaultLanguageRule = "Luôn trả lời bằng tiếng Việt, thân thiện, tự nhiên như nhân viên tư vấn homestay.";
        private const string DefaultDataTruthRule = "Không bịa phòng trống, giá, chính sách hoặc thông tin chi nhánh. Chỉ dùng dữ liệu từ Live System, Knowledge, Graph và cấu hình được cung cấp.";
        private const string DefaultMissingInfoRule = "Nếu thiếu ngày/giờ/chi nhánh/số khách theo cấu hình form đã lọc, hãy hỏi lại bằng đúng các trường cần điền. Không hỏi ngân sách vì giá phòng đã cố định trong hệ thống.";
        private const string DefaultBookingRule = "Không xác nhận đặt phòng, không hứa giữ phòng, không tạo mã khóa/check-in code; chỉ hướng khách sang luồng đặt phòng chính thức.";
        private const string DefaultFormRule = "Form Schema JSON đã được lọc theo điều kiện của từng field cho câu hỏi hiện tại. Nếu Form Schema còn trường, hãy hỏi khách điền đúng các trường đó, không thêm trường ngoài schema. Nếu Form Schema rỗng nhưng nhu cầu chưa rõ, hãy hỏi thêm một câu ngắn để xác định intent trước khi xin thông tin.";
        private const string DefaultPaymentRule = "Nếu field type là paymentQr, được gửi đúng messageTemplate và qrImageUrl đã cấu hình như hướng dẫn chuyển khoản; không tự xác nhận booking sau khi gửi QR.";
        private const string DefaultMemoryRule = "Phải ghi nhớ các thông tin khách đã nói trong lịch sử cùng session; không hỏi lại chi nhánh, ngày giờ, số khách nếu khách đã cung cấp rồi.";
        private const string DefaultContextFormatRule = "Đọc Persona Agent, Safety Guard, Live System Agent JSON, Knowledge RAG Agent JSON và Graph Reasoning Agent JSON như dữ liệu nội bộ để tổng hợp câu trả lời cuối cùng; không hiển thị raw JSON cho khách.";
        private const string DefaultBookingFormSchema = """
[
  {
    "id": "customerName",
    "type": "text",
    "label": "Họ và tên",
    "required": true,
    "helpText": "Nhập đúng họ tên người đặt phòng.",
    "order": 1
  },
  {
    "id": "customerPhone",
    "type": "tel",
    "label": "Số điện thoại",
    "required": true,
    "helpText": "Số điện thoại/Zalo để homestay liên hệ xác nhận.",
    "order": 2
  },
  {
    "id": "customerEmail",
    "type": "email",
    "label": "Email",
    "required": false,
    "helpText": "Email nhận thông tin đặt phòng nếu có.",
    "order": 3
  },
  {
    "id": "guestCount",
    "type": "number",
    "label": "Số khách",
    "required": true,
    "helpText": "Tổng số khách lưu trú.",
    "order": 4
  },
  {
    "id": "idCardFront",
    "type": "image",
    "label": "Ảnh CCCD mặt trước",
    "required": true,
    "helpText": "Upload ảnh rõ nét mặt trước CCCD/CMND/Hộ chiếu.",
    "order": 5
  },
  {
    "id": "idCardBack",
    "type": "image",
    "label": "Ảnh CCCD mặt sau",
    "required": true,
    "helpText": "Upload ảnh rõ nét mặt sau CCCD/CMND nếu có.",
    "order": 6
  },
  {
    "id": "customerNote",
    "type": "textarea",
    "label": "Ghi chú thêm",
    "required": false,
    "helpText": "Yêu cầu đặc biệt, giờ đến dự kiến hoặc ghi chú khác.",
    "order": 7
  }
]
""";

        private readonly ApplicationDbContext _context;
        private readonly IAIModelClient _aiModelClient;
        private readonly IAIBrainOrchestrator _aiBrainOrchestrator;

        public AdminAIController(ApplicationDbContext context, IAIModelClient aiModelClient, IAIBrainOrchestrator aiBrainOrchestrator)
        {
            _context = context;
            _aiModelClient = aiModelClient;
            _aiBrainOrchestrator = aiBrainOrchestrator;
        }




        [AdminAuthorize(Permission = "ai.response")]
        [HttpGet("final-synthesizer-config")]
        public async Task<IActionResult> GetFinalSynthesizerConfig()
        {
            var settings = await _context.SystemSettings
                .Where(s => s.GroupName == "AI")
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

            return Ok(new
            {
                style = GetAISetting(settings, "AIFinalSynthesizerStyle", DefaultFinalStyle),
                formSchema = GetAISetting(settings, "AIFinalSynthesizerFormSchema", "[]"),
                conditionOptions = GetAISetting(settings, "AIFinalConditionOptions", string.Empty),
                basePrompt = GetAISetting(settings, "AIFinalBasePrompt", DefaultBasePrompt),
                languageRule = GetAISetting(settings, "AIFinalLanguageRule", DefaultLanguageRule),
                dataTruthRule = GetAISetting(settings, "AIFinalDataTruthRule", DefaultDataTruthRule),
                missingInfoRule = GetAISetting(settings, "AIFinalMissingInfoRule", DefaultMissingInfoRule),
                bookingRule = GetAISetting(settings, "AIFinalBookingRule", DefaultBookingRule),
                formRule = GetAISetting(settings, "AIFinalFormRule", DefaultFormRule),
                paymentRule = GetAISetting(settings, "AIFinalPaymentRule", DefaultPaymentRule),
                memoryRule = GetAISetting(settings, "AIFinalMemoryRule", DefaultMemoryRule),
                contextFormatRule = GetAISetting(settings, "AIFinalContextFormatRule", DefaultContextFormatRule)
            });
        }

        [AdminAuthorize(Permission = "ai.response")]
        [HttpGet("booking-form-config")]
        public async Task<IActionResult> GetBookingFormConfig()
        {
            var settings = await _context.SystemSettings
                .Where(s => s.GroupName == "AI")
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

            return Ok(new
            {
                formSchema = GetAISetting(settings, "AIBookingFormSchema", DefaultBookingFormSchema)
            });
        }

        [AdminAuthorize(Permission = "ai.edit")]
        [HttpPost("booking-form-config")]
        public async Task<IActionResult> SaveBookingFormConfig([FromBody] BookingFormConfigRequest request)
        {
            if (request == null)
            {
                return BadRequest("Booking form config request is required.");
            }

            var formSchema = request.FormSchema ?? DefaultBookingFormSchema;
            JsonDocument parsedSchema;
            try
            {
                parsedSchema = JsonDocument.Parse(formSchema);
            }
            catch (JsonException)
            {
                return BadRequest("Booking form schema must be valid JSON.");
            }

            using (parsedSchema)
            {
                if (parsedSchema.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return BadRequest("Booking form schema root must be an array.");
                }
            }

            await UpsertAISetting("AIBookingFormSchema", formSchema, "Schema form đặt phòng dùng cho public AI booking flow");
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [AdminAuthorize(Permission = "ai.edit")]
        [HttpPost("final-synthesizer-config")]
        public async Task<IActionResult> SaveFinalSynthesizerConfig([FromBody] FinalSynthesizerConfigRequest request)
        {
            await UpsertAISetting("AIFinalSynthesizerStyle", request.Style ?? string.Empty, "Phong cách trả lời của Final Response Synthesizer");
            await UpsertAISetting("AIFinalSynthesizerFormSchema", request.FormSchema ?? "[]", "Schema form gợi ý cho chatbot preview/user input");
            await UpsertAISetting("AIFinalConditionOptions", request.ConditionOptions ?? string.Empty, "Option điều kiện hiển thị field của Final Response Synthesizer");
            await UpsertAISetting("AIFinalBasePrompt", request.BasePrompt ?? string.Empty, "System prompt vai trò chính của Final Response Synthesizer");
            await UpsertAISetting("AIFinalLanguageRule", request.LanguageRule ?? string.Empty, "Quy tắc ngôn ngữ và giọng nói của AI");
            await UpsertAISetting("AIFinalDataTruthRule", request.DataTruthRule ?? string.Empty, "Quy tắc chống bịa dữ liệu của AI");
            await UpsertAISetting("AIFinalMissingInfoRule", request.MissingInfoRule ?? string.Empty, "Quy tắc hỏi thông tin thiếu của AI");
            await UpsertAISetting("AIFinalBookingRule", request.BookingRule ?? string.Empty, "Quy tắc chốt phòng và booking của AI");
            await UpsertAISetting("AIFinalFormRule", request.FormRule ?? string.Empty, "Quy tắc dùng form schema của AI");
            await UpsertAISetting("AIFinalPaymentRule", request.PaymentRule ?? string.Empty, "Quy tắc thanh toán và QR của AI");
            await UpsertAISetting("AIFinalMemoryRule", request.MemoryRule ?? string.Empty, "Quy tắc ghi nhớ hội thoại của AI");
            await UpsertAISetting("AIFinalContextFormatRule", request.ContextFormatRule ?? string.Empty, "Quy tắc đọc context agent của AI");
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [AdminAuthorize(Permission = "ai.response")]
        [HttpGet("public-booking-config")]
        public async Task<IActionResult> GetPublicBookingConfig()
        {
            var settings = await _context.SystemSettings
                .Where(s => s.GroupName == "AI")
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

            return Ok(new
            {
                prompt = GetAISetting(settings, "AIPublicBookingPrompt", string.Empty),
                triggerWords = GetAISetting(settings, "AIPublicBookingTriggerWords", "đặt,chốt,lấy,book,giữ phòng"),
                maxTokens = GetAISetting(settings, "AIPublicBookingMaxTokens", "300"),
                timeout = GetAISetting(settings, "AIPublicBookingTimeout", "15")
            });
        }

        [AdminAuthorize(Permission = "ai.edit")]
        [HttpPost("public-booking-config")]
        public async Task<IActionResult> SavePublicBookingConfig([FromBody] PublicBookingConfigRequest request)
        {
            await UpsertAISetting("AIPublicBookingPrompt", request.Prompt ?? string.Empty, "System prompt bổ sung cho Public Booking mode");
            await UpsertAISetting("AIPublicBookingTriggerWords", request.TriggerWords ?? "đặt,chốt,lấy,book,giữ phòng", "Từ khoá phát hiện booking intent (phân cách bằng dấu phẩy)");
            await UpsertAISetting("AIPublicBookingMaxTokens", request.MaxTokens ?? "300", "Max tokens cho public booking mode");
            await UpsertAISetting("AIPublicBookingTimeout", request.Timeout ?? "15", "Timeout (giây) cho public booking mode");
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        private string GetAISetting(Dictionary<string, string> settings, string key, string fallback)
        {
            return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
        }

        private async Task UpsertAISetting(string key, string value, string description)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null)
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    Description = description,
                    GroupName = "AI",
                    LastUpdated = DateTime.Now
                });
                return;
            }

            setting.SettingValue = value;
            setting.Description = description;
            setting.GroupName = "AI";
            setting.LastUpdated = DateTime.Now;
        }

        [AdminAuthorize(Permission = "ai.edit")]
        [HttpPost("upload-payment-qr")]
        public async Task<IActionResult> UploadPaymentQr(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Chưa chọn ảnh QR.");
            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return BadRequest("File phải là ảnh.");

            var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "ai-payment");
            Directory.CreateDirectory(uploadFolder);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10) extension = ".png";
            extension = Regex.Replace(extension, "[^a-z0-9.]", string.Empty);
            var fileName = $"qr-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(uploadFolder, fileName);

            await using var stream = System.IO.File.Create(path);
            await file.CopyToAsync(stream);

            return Ok(new { url = $"/uploads/ai-payment/{fileName}" });
        }

        [AdminAuthorize(Permission = "ai.trace")]
        [HttpPost("test-agent")]
        public async Task<IActionResult> TestAgent([FromBody] AIAgentTestRequest request)
        {
            var agent = request.AgentKey?.Trim().ToLowerInvariant() ?? string.Empty;
            var result = agent switch
            {
                "orchestrator" => await TestOrchestratorAgent(request),
                "live" => await TestLiveSystemAgent(request),
                "knowledge" => await TestKnowledgeAgent(request),
                "graph" => await TestGraphAgent(request),
                "persona" => TestPersonaAgent(request),
                "guard" => TestGuardAgent(request),
                _ => new { agent = "unknown", ok = false, message = "Agent không hợp lệ." }
            };

            return Ok(result);
        }

        private async Task<object> TestOrchestratorAgent(AIAgentTestRequest request)
        {
            var requiredAgents = new List<string> { "persona", "guard" };
            if (request.BranchId.HasValue && request.StartTime.HasValue && request.EndTime.HasValue) requiredAgents.Add("live");
            if (!string.IsNullOrWhiteSpace(request.Message)) requiredAgents.Add("knowledge");
            requiredAgents.Add("graph");
            requiredAgents.Add("synthesizer");

            return new
            {
                agent = "orchestrator",
                ok = true,
                input = request.Message,
                decision = "Chia câu hỏi thành các bước xử lý độc lập rồi gom lại trước khi trả lời.",
                plannedFlow = requiredAgents,
                nextAction = "Gọi từng agent, kiểm tra guard, sau đó lưu trace."
            };
        }

        private async Task<object> TestLiveSystemAgent(AIAgentTestRequest request)
        {
            if (!request.BranchId.HasValue || !request.StartTime.HasValue || !request.EndTime.HasValue)
            {
                var branches = await _context.Branches.Select(b => new { b.Id, b.Name, b.Address, b.Hotline }).ToListAsync();
                return new { agent = "live", ok = false, missing = "Cần branchId, startTime, endTime để kiểm tra phòng trống.", branches };
            }

            var availableIds = await _context.Rooms
                .Where(r => r.BranchId == request.BranchId.Value && r.Status == "Available" && r.MaxGuests >= request.GuestCount)
                .Select(r => r.Id)
                .ToListAsync();

            var rooms = await _context.Rooms
                .Where(r => availableIds.Contains(r.Id))
                .Include(r => r.Branch)
                .Select(r => new { r.Id, r.Name, r.PricePerHour, r.PricePerDay, r.Capacity, r.MaxGuests, Branch = r.Branch!.Name })
                .ToListAsync();

            return new { agent = "live", ok = true, request.BranchId, request.StartTime, request.EndTime, request.GuestCount, rooms };
        }

        private async Task<object> TestKnowledgeAgent(AIAgentTestRequest request)
        {
            var terms = (request.Message ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(t => t.Length >= 3).Take(8).ToList();
            var units = await _context.AIKnowledgeUnits
                .Where(k => k.IsActive)
                .OrderByDescending(k => k.Priority)
                .ThenByDescending(k => k.LastUpdated)
                .Take(30)
                .Select(k => new { k.Id, k.Title, k.Content, k.Tags, k.Priority })
                .ToListAsync();
            var matched = units.Where(k => terms.Count == 0 || terms.Any(t => k.Title.Contains(t, StringComparison.OrdinalIgnoreCase) || k.Content.Contains(t, StringComparison.OrdinalIgnoreCase) || k.Tags.Contains(t, StringComparison.OrdinalIgnoreCase))).Take(5).ToList();
            return new { agent = "knowledge", ok = true, terms, matchedCount = matched.Count, matched };
        }

        private async Task<object> TestGraphAgent(AIAgentTestRequest request)
        {
            var nodes = await _context.AIGraphNodes.Where(n => n.IsActive).Select(n => new { n.Id, n.NodeType, n.Label, n.Summary }).ToListAsync();
            var matchedNodes = nodes.Where(n => (request.Message ?? string.Empty).Contains(n.Label, StringComparison.OrdinalIgnoreCase) || n.Summary.Contains(request.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase)).Take(5).ToList();
            var nodeIds = matchedNodes.Select(n => n.Id).ToList();
            var edges = await _context.AIGraphEdges
                .Where(e => nodeIds.Contains(e.FromNodeId) || nodeIds.Contains(e.ToNodeId))
                .Include(e => e.FromNode)
                .Include(e => e.ToNode)
                .Select(e => new { From = e.FromNode.Label, To = e.ToNode.Label, e.RelationshipType, e.Weight, e.Evidence })
                .Take(10)
                .ToListAsync();
            return new { agent = "graph", ok = true, matchedNodes, edges, hint = matchedNodes.Any() ? "Graph có dữ liệu liên quan." : "Chưa match node; cần thêm label/summary sát cách khách hỏi." };
        }

        private object TestPersonaAgent(AIAgentTestRequest request)
        {
            var message = request.Message?.ToLowerInvariant() ?? string.Empty;
            var signals = new List<string>();
            if (message.Contains("rẻ") || message.Contains("giá")) signals.Add("price-sensitive");
            if (message.Contains("gấp") || message.Contains("hôm nay") || message.Contains("ngay")) signals.Add("urgent");
            if (message.Contains("view") || message.Contains("đẹp") || message.Contains("chill")) signals.Add("experience-oriented");
            if (request.GuestCount >= 3) signals.Add("group");
            if (!signals.Any()) signals.Add("general-booking");
            return new { agent = "persona", ok = true, signals, summary = $"Khách thuộc nhóm {string.Join(", ", signals)}; nên tư vấn ngắn, rõ lựa chọn và hỏi thêm thông tin thiếu." };
        }

        private object TestGuardAgent(AIAgentTestRequest request)
        {
            var warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(request.Message)) warnings.Add("Câu hỏi trống.");
            if (request.StartTime.HasValue && request.EndTime.HasValue && request.EndTime <= request.StartTime) warnings.Add("Khoảng thời gian không hợp lệ.");
            if (!request.BranchId.HasValue) warnings.Add("Thiếu chi nhánh, không nên khẳng định phòng trống.");
            if (!request.StartTime.HasValue || !request.EndTime.HasValue) warnings.Add("Thiếu thời gian, không nên báo availability.");
            return new { agent = "guard", ok = !warnings.Any(), warnings, policy = "Không xác nhận booking, không bịa giá/phòng trống, không tạo mã khóa/check-in code." };
        }

        [AdminAuthorize(Permission = "ai.view")]
        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
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
                            k.LastUpdated
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
            if (scope.Id == Guid.Empty) scope.Id = Guid.NewGuid();

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
            if (unit.Id == Guid.Empty) unit.Id = Guid.NewGuid();

            var existing = await _context.AIKnowledgeUnits.FindAsync(unit.Id);
            if (existing == null)
            {
                unit.LastUpdated = DateTime.Now;
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
                existing.LastUpdated = DateTime.Now;
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
            _context.AIKnowledgeUnits.Remove(unit);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [AdminAuthorize(Permission = "ai.graph")]
        [HttpGet("brain-graph")]
        public async Task<IActionResult> GetBrainGraph()
        {
            var nodes = await _context.AIGraphNodes
                .OrderBy(n => n.NodeType)
                .ThenBy(n => n.Label)
                .Select(n => new { n.Id, n.NodeType, n.Label, n.Summary, n.MetadataJson, n.IsActive })
                .ToListAsync();

            var edges = await _context.AIGraphEdges
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
            if (node.Id == Guid.Empty) node.Id = Guid.NewGuid();

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
            _context.AIGraphNodes.Remove(node);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [AdminAuthorize(Permission = "ai.create")]
        [HttpPost("brain-graph-edge")]
        public async Task<IActionResult> SaveBrainGraphEdge([FromBody] AIGraphEdge edge)
        {
            if (edge.Id == Guid.Empty) edge.Id = Guid.NewGuid();

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
            _context.AIGraphEdges.Remove(edge);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [AdminAuthorize(Permission = "ai.trace")]
        [HttpGet("brain-traces")]
        public async Task<IActionResult> GetBrainTraces()
        {
            var traces = await _context.AIConversationTraces
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .Select(t => new
                {
                    t.Id,
                    t.CreatedAt,
                    t.CustomerMessage,
                    t.ModelProvider,
                    t.GuardResult,
                    AnswerPreview = t.FinalAnswer.Length > 120 ? t.FinalAnswer.Substring(0, 120) + "..." : t.FinalAnswer
                })
                .ToListAsync();

            return Ok(traces);
        }

        [AdminAuthorize(Permission = "ai.detail")]
        [HttpGet("brain-trace/{id}")]
        public async Task<IActionResult> GetBrainTrace(Guid id)
        {
            var trace = await _context.AIConversationTraces.FindAsync(id);
            if (trace == null) return NotFound();
            return Ok(trace);
        }

        [AdminAuthorize(Permission = "ai.edit")]
        [HttpPost("seed-brain-data")]
        public async Task<IActionResult> SeedBrainData()
        {
            var createdScopes = 0;
            var createdKnowledgeUnits = 0;
            var createdNodes = 0;
            var createdEdges = 0;

            var systemScope = await _context.AIBrainScopes.FirstOrDefaultAsync(s => s.Name == "Tri thức vận hành hiện có");
            if (systemScope == null)
            {
                systemScope = new AIBrainScope
                {
                    Name = "Tri thức vận hành hiện có",
                    Description = "Dữ liệu được đồng bộ từ AI Knowledge Hub, chi nhánh và phòng để Brain Center tư vấn.",
                    Order = 1
                };
                _context.AIBrainScopes.Add(systemScope);
                createdScopes++;
            }

            var articles = await _context.AIKnowledgeArticles.Include(a => a.Collection).ToListAsync();
            if (!articles.Any() && !await _context.AIKnowledgeUnits.AnyAsync(k => k.ScopeId == systemScope.Id))
            {
                var defaultKnowledge = new[]
                {
                    new AIKnowledgeUnit
                    {
                        ScopeId = systemScope.Id,
                        Title = "Tư vấn phòng theo nhu cầu khách",
                        Content = "Khi khách hỏi phòng, cần hỏi đủ chi nhánh, thời gian nhận/trả, số khách và ngân sách. Chỉ gợi ý phòng xuất hiện trong Live System Snapshot.",
                        Tags = "sales,availability,policy",
                        Priority = 20
                    },
                    new AIKnowledgeUnit
                    {
                        ScopeId = systemScope.Id,
                        Title = "Quy tắc an toàn khi xác nhận đặt phòng",
                        Content = "AI không được tự xác nhận đặt phòng, không hứa giữ phòng và không tạo mã khóa. Luôn hướng khách sang luồng đặt phòng chính thức sau khi tư vấn.",
                        Tags = "safety,booking,policy",
                        Priority = 30
                    },
                    new AIKnowledgeUnit
                    {
                        ScopeId = systemScope.Id,
                        Title = "Chiến thuật upsell mềm",
                        Content = "Nếu khách đi nhóm hoặc cần trải nghiệm đẹp, ưu tiên nêu lợi ích về sức chứa, view, tiện nghi và sự thuận tiện thay vì chỉ nói giá.",
                        Tags = "sales,upsell,persona",
                        Priority = 15
                    }
                };

                _context.AIKnowledgeUnits.AddRange(defaultKnowledge);
                createdKnowledgeUnits += defaultKnowledge.Length;
            }

            foreach (var article in articles)
            {
                var exists = await _context.AIKnowledgeUnits.AnyAsync(k => k.Title == article.Title && k.ScopeId == systemScope.Id);
                if (exists) continue;

                _context.AIKnowledgeUnits.Add(new AIKnowledgeUnit
                {
                    ScopeId = systemScope.Id,
                    Title = article.Title,
                    Content = article.Content,
                    Tags = article.Collection != null ? article.Collection.Name : "Knowledge Hub",
                    Priority = 10,
                    LastUpdated = article.LastUpdated
                });
                createdKnowledgeUnits++;
            }

            var branches = await _context.Branches.Include(b => b.Rooms).ToListAsync();
            foreach (var branch in branches)
            {
                var branchNode = await _context.AIGraphNodes.FirstOrDefaultAsync(n => n.NodeType == "branch" && n.Label == branch.Name);
                if (branchNode == null)
                {
                    branchNode = new AIGraphNode
                    {
                        NodeType = "branch",
                        Label = branch.Name,
                        Summary = $"Chi nhánh tại {branch.Address}. Hotline: {branch.Hotline}. Lead time đặt phòng: {branch.BookingLeadTimeHours} giờ."
                    };
                    _context.AIGraphNodes.Add(branchNode);
                    createdNodes++;
                }

                foreach (var room in branch.Rooms)
                {
                    var roomNode = await _context.AIGraphNodes.FirstOrDefaultAsync(n => n.NodeType == "room" && n.Label == room.Name);
                    if (roomNode == null)
                    {
                        roomNode = new AIGraphNode
                        {
                            NodeType = "room",
                            Label = room.Name,
                            Summary = $"Phòng sức chứa {room.Capacity}, tối đa {room.MaxGuests} khách, giá giờ {room.PricePerHour:N0}, giá ngày {room.PricePerDay:N0}. Trạng thái: {room.Status}."
                        };
                        _context.AIGraphNodes.Add(roomNode);
                        createdNodes++;
                    }

                    var edgeExists = await _context.AIGraphEdges.AnyAsync(e => e.FromNodeId == branchNode.Id && e.ToNodeId == roomNode.Id && e.RelationshipType == "contains_room");
                    if (!edgeExists)
                    {
                        _context.AIGraphEdges.Add(new AIGraphEdge
                        {
                            FromNodeId = branchNode.Id,
                            ToNodeId = roomNode.Id,
                            RelationshipType = "contains_room",
                            Weight = 1,
                            Evidence = "Đồng bộ từ dữ liệu Branch.Rooms hiện có."
                        });
                        createdEdges++;
                    }
                }
            }

            await _context.SaveChangesAsync();

            var totalScopes = await _context.AIBrainScopes.CountAsync();
            var totalKnowledgeUnits = await _context.AIKnowledgeUnits.CountAsync();
            var totalNodes = await _context.AIGraphNodes.CountAsync();
            var totalEdges = await _context.AIGraphEdges.CountAsync();

            return Ok(new
            {
                success = true,
                createdScopes,
                createdKnowledgeUnits,
                createdNodes,
                createdEdges,
                totalScopes,
                totalKnowledgeUnits,
                totalNodes,
                totalEdges
            });
        }

        [AdminAuthorize(Permission = "ai.trace")]
        [HttpPost("brain-chat")]
        public async Task<IActionResult> BrainChat([FromBody] AIBrainChatRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _aiBrainOrchestrator.ChatAsync(request, cancellationToken);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    exceptionType = ex.GetType().FullName,
                    stackTrace = ex.StackTrace,
                    innerException = ex.InnerException?.Message,
                    innerExceptionType = ex.InnerException?.GetType().FullName
                });
            }
        }

        [AdminAuthorize(Permission = "ai.trace")]
        [HttpPost("brain-preview")]
        public async Task<IActionResult> BrainPreview([FromBody] AIModelRequest request, CancellationToken cancellationToken)
        {
            var response = await _aiModelClient.CompleteAsync(new AIModelRequest
            {
                SystemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt)
                    ? "Bạn là Hospitality Brain đa tác nhân cho hệ thống homestay self check-in/self check-out. Tư vấn ngắn gọn, đúng chính sách và không bịa dữ liệu phòng trống."
                    : request.SystemPrompt,
                UserMessage = request.UserMessage,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens
            }, cancellationToken);

            return Ok(response);
        }

        [AdminAuthorize(Permission = "ai.knowledge")]
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

        [AdminAuthorize(Permission = "ai.edit")]
        [HttpPost("save-collection")]
        public async Task<IActionResult> SaveCollection([FromBody] AIKnowledgeCollection collection)
        {
            if (collection.Id == Guid.Empty) collection.Id = Guid.NewGuid();

            var existing = await _context.AIKnowledgeCollections.FindAsync(collection.Id);
            if (existing == null)
            {
                _context.AIKnowledgeCollections.Add(collection);
            }
            else
            {
                existing.Name = collection.Name;
                existing.Icon = collection.Icon;
                existing.Description = collection.Description;
                existing.Order = collection.Order;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = collection.Id });
        }

        [AdminAuthorize(Permission = "ai.delete")]
        [HttpDelete("collection/{id}")]
        public async Task<IActionResult> DeleteCollection(Guid id)
        {
            var collection = await _context.AIKnowledgeCollections.FindAsync(id);
            if (collection == null) return NotFound();

            // Also delete all articles in this collection
            var articles = _context.AIKnowledgeArticles.Where(a => a.CollectionId == id);
            _context.AIKnowledgeArticles.RemoveRange(articles);

            _context.AIKnowledgeCollections.Remove(collection);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [AdminAuthorize(Permission = "ai.knowledge")]
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

        [AdminAuthorize(Permission = "ai.detail")]
        [HttpGet("article/{id}")]
        public async Task<IActionResult> GetArticle(Guid id)
        {
            var article = await _context.AIKnowledgeArticles.FindAsync(id);
            if (article == null) return NotFound();
            return Ok(article);
        }

        [AdminAuthorize(Permission = "ai.edit")]
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

        [AdminAuthorize(Permission = "ai.delete")]
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
