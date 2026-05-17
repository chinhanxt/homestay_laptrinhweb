using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    [Route("ai")]
    public class AIChatController : Controller
    {
        private readonly IAIBrainOrchestrator _aiBrainOrchestrator;
        private readonly ApplicationDbContext _context;

        public AIChatController(IAIBrainOrchestrator aiBrainOrchestrator, ApplicationDbContext context)
        {
            _aiBrainOrchestrator = aiBrainOrchestrator;
            _context = context;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] PublicAIChatRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new
                {
                    answer = "Bạn nhập giúp mình nhu cầu đặt phòng để mình tư vấn nhé.",
                    message = "Bạn nhập giúp mình nhu cầu đặt phòng để mình tư vấn nhé.",
                    sessionId = request?.SessionId ?? string.Empty,
                    currentStep = "intent",
                    state = new AIBookingSessionState(),
                    uiBlocks = Array.Empty<AIUiBlock>(),
                    formSchema = "[]"
                });
            }

            var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId.Trim();

            try
            {
                var message = request.Message.Trim();
                var guestCount = ResolveGuestCount(message, request.GuestCount);
                var branchId = request.BranchId ?? await ResolveBranchIdAsync(message, cancellationToken);
                var hasDate = TryExtractDate(message, out var parsedDate);
                var response = await _aiBrainOrchestrator.ChatAsync(new AIBrainChatRequest
                {
                    SessionId = sessionId,
                    Message = message,
                    BranchId = branchId,
                    StartTime = hasDate ? parsedDate.ToDateTime(TimeOnly.MinValue) : request.StartTime,
                    EndTime = hasDate ? parsedDate.ToDateTime(TimeOnly.MaxValue) : request.EndTime,
                    GuestCount = guestCount
                }, cancellationToken);

                var uiBlocks = new List<AIUiBlock>();
                if (branchId.HasValue && hasDate)
                {
                    var rooms = await BuildRoomCardsAsync(branchId.Value, parsedDate, guestCount, cancellationToken);
                    if (rooms.Any()) uiBlocks.Add(new AIUiBlock { Type = "roomCards", Data = new { rooms } });
                }

                return Ok(new
                {
                    answer = response.Answer,
                    message = response.Answer,
                    sessionId,
                    currentStep = "chat",
                    state = new AIBookingSessionState
                    {
                        BranchId = branchId,
                        HourlyDate = hasDate ? parsedDate : null,
                        GuestCount = guestCount
                    },
                    uiBlocks,
                    formSchema = response.FormSchema
                });
            }
            catch
            {
                return StatusCode(503, new
                {
                    answer = "Xin lỗi, trợ lý AI đang tạm thời bận. Bạn thử lại sau ít phút nhé.",
                    message = "Xin lỗi, trợ lý AI đang tạm thời bận. Bạn thử lại sau ít phút nhé.",
                    sessionId,
                    currentStep = "error",
                    state = new AIBookingSessionState(),
                    uiBlocks = Array.Empty<AIUiBlock>(),
                    formSchema = "[]"
                });
            }
        }

        private async Task<int?> ResolveBranchIdAsync(string text, CancellationToken cancellationToken)
        {
            var lowered = text.ToLowerInvariant();
            var branches = await _context.Branches.ToListAsync(cancellationToken);
            return branches.FirstOrDefault(b => lowered.Contains(b.Name.ToLowerInvariant()))?.Id
                ?? branches.FirstOrDefault(b => b.Name.Contains("Sài Gòn") && (lowered.Contains("sài gòn") || lowered.Contains("sai gon") || lowered.Contains("saigon") || lowered.Contains("sg") || lowered.Contains("hcm") || lowered.Contains("tphcm")))?.Id
                ?? branches.FirstOrDefault(b => b.Name.Contains("Đà Lạt") && (lowered.Contains("đà lạt") || lowered.Contains("da lat") || lowered.Contains("dalat") || lowered.Contains("dl")))?.Id;
        }

        private int ResolveGuestCount(string text, int requestGuestCount)
        {
            var match = Regex.Match(text, @"(\d+)\s*(ng|người|nguoi|khách|khach)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedGuests) && parsedGuests > 0) return parsedGuests;
            return requestGuestCount <= 0 ? 1 : requestGuestCount;
        }

        private bool TryExtractDate(string text, out DateOnly date)
        {
            var lowered = text.ToLowerInvariant();
            if (lowered.Contains("hôm nay") || lowered.Contains("hom nay"))
            {
                date = DateOnly.FromDateTime(DateTime.Today);
                return true;
            }
            if (lowered.Contains("ngày mai") || lowered.Contains("ngay mai"))
            {
                date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
                return true;
            }

            var match = Regex.Match(lowered, @"(?:ngày\s*)?(\d{1,2})[/-](\d{1,2})(?:[/-](\d{2,4}))?");
            if (match.Success)
            {
                var day = int.Parse(match.Groups[1].Value);
                var month = int.Parse(match.Groups[2].Value);
                var year = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : DateTime.Today.Year;
                if (year < 100) year += 2000;
                return DateOnly.TryParse($"{year:D4}-{month:D2}-{day:D2}", out date);
            }

            var dayOnlyMatch = Regex.Match(lowered, @"ngày\s+(\d{1,2})(?!\s*[/-])");
            if (dayOnlyMatch.Success)
            {
                var day = int.Parse(dayOnlyMatch.Groups[1].Value);
                return DateOnly.TryParse($"{DateTime.Today.Year:D4}-{DateTime.Today.Month:D2}-{day:D2}", out date);
            }

            date = default;
            return false;
        }

        private async Task<List<AIRoomCard>> BuildRoomCardsAsync(int branchId, DateOnly date, int guestCount, CancellationToken cancellationToken)
        {
            return await _context.Rooms
                .Where(r => r.BranchId == branchId && r.Status == "Available" && r.MaxGuests >= Math.Max(guestCount, 1))
                .Include(r => r.Amenities)
                .OrderBy(r => r.PricePerHour)
                .Select(r => new AIRoomCard
                {
                    RoomId = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    PricePerHour = r.PricePerHour,
                    PricePerDay = r.PricePerDay,
                    Capacity = r.Capacity,
                    MaxGuests = r.MaxGuests,
                    ExtraGuestFee = r.ExtraGuestFee,
                    ImageUrl = r.ImageUrl,
                    Amenities = r.Amenities.Select(a => a.Name).ToList(),
                    DetailsUrl = $"/Rooms/Details/{r.Id}?hourlyDate={date:yyyy-MM-dd}"
                })
                .ToListAsync(cancellationToken);
        }
    }}
