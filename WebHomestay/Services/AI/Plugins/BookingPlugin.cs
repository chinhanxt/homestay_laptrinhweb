using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using WebHomestay.Data;
using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Services.Slots;

namespace WebHomestay.Services.AI.Plugins
{
    public class BookingPlugin
    {
        private readonly ApplicationDbContext _context;
        private readonly IAvailabilityService _availabilityService;
        private readonly ISlotGenerationService _slotGenerationService;
        private readonly AIBookingSessionState _sessionState;
        private readonly AIBrainChatRequest _request;
        private readonly StringBuilder _logs;
        private string? _bookingAction;
        private readonly List<object> _uiBlocks;

        public BookingPlugin(
            ApplicationDbContext context,
            IAvailabilityService availabilityService,
            ISlotGenerationService slotGenerationService,
            AIBookingSessionState sessionState,
            AIBrainChatRequest request)
        {
            _context = context;
            _availabilityService = availabilityService;
            _slotGenerationService = slotGenerationService;
            _sessionState = sessionState;
            _request = request;
            _logs = new StringBuilder();
            _uiBlocks = new List<object>();
        }

        [KernelFunction("CheckAvailability")]
        [Description("Tìm kiếm phòng trống tại một chi nhánh trong một khoảng thời gian hoặc theo ngày.")]
        public async Task<string> CheckAvailability(
            [Description("Tên chi nhánh (VD: Q1, Đà Lạt)")] string? branchName = null,
            [Description("Ngày nhận phòng (YYYY-MM-DD)")] string? checkInDate = null,
            [Description("Ngày trả phòng (YYYY-MM-DD), bỏ trống nếu thuê theo giờ")] string? checkOutDate = null,
            [Description("Số khách")] int guests = 1)
        {
            _logs.AppendLine($"[CheckAvailability] branchName={branchName}, checkInDate={checkInDate}, checkOutDate={checkOutDate}, guests={guests}");
            
            var branches = await _context.Branches.ToListAsync();
            if (string.IsNullOrWhiteSpace(branchName))
            {
                var branchNames = string.Join(", ", branches.Select(b => b.Name));
                return $"Hãy yêu cầu khách chọn một trong các chi nhánh: {branchNames}.";
            }
            var matchedBranches = branches.Where(b => b.Name.Contains(branchName, StringComparison.OrdinalIgnoreCase) 
                || branchName.Contains(b.Name, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchedBranches.Count == 0)
            {
                var branchNames = string.Join(", ", branches.Select(b => b.Name));
                return $"Không tìm thấy chi nhánh nào khớp với '{branchName}'. Các chi nhánh hiện có: {branchNames}. Hãy yêu cầu khách chọn một chi nhánh.";
            }
            if (matchedBranches.Count > 1)
            {
                var branchNames = string.Join(", ", matchedBranches.Select(b => b.Name));
                return $"Tìm thấy {matchedBranches.Count} chi nhánh khớp với '{branchName}': {branchNames}. Hãy yêu cầu khách chỉ định rõ tên chi nhánh muốn đặt.";
            }

            var branch = matchedBranches.First();

            if (string.IsNullOrWhiteSpace(checkInDate) || !DateTime.TryParse(checkInDate, out var start))
            {
                return "Ngày nhận phòng không hợp lệ hoặc chưa được cung cấp. Hãy hỏi khách ngày nhận phòng.";
            }

            DateTime end;
            if (!string.IsNullOrWhiteSpace(checkOutDate) && DateTime.TryParse(checkOutDate, out var parsedEnd))
            {
                end = parsedEnd;
            }
            else
            {
                // Default to hourly mode or just same day
                end = start.AddDays(1);
            }

            var availableIds = await _availabilityService.GetAvailableRoomIds(branch.Id, start, end);
            
            if (!availableIds.Any())
            {
                return $"Không có phòng nào trống tại {branch.Name} cho {guests} khách vào thời gian này.";
            }

            var availableRooms = await _context.Rooms
                .Where(r => availableIds.Contains(r.Id) && r.MaxGuests >= guests)
                .ToListAsync();

            var roomData = availableRooms.Select(r => new
            {
                roomId = r.Id,
                name = r.Name,
                description = r.Description,
                pricePerHour = r.PricePerHour,
                pricePerDay = r.PricePerDay,
                capacity = r.Capacity,
                maxGuests = r.MaxGuests,
                extraGuestFee = r.ExtraGuestFee,
                detailsUrl = $"/Rooms/Details/{r.Id}"
            }).ToList();

            _bookingAction = "showRooms";
            _uiBlocks.Add(new { type = "roomCards", data = new { rooms = roomData } });

            var resultStr = $"Tìm thấy {roomData.Count} phòng trống: " + JsonSerializer.Serialize(roomData);
            _logs.AppendLine($"[Result CheckAvailability] {resultStr}");
            return resultStr;
        }

        [KernelFunction("ShowRoomCards")]
        [Description("Hiển thị các thẻ phòng (UI room cards) cho danh sách ID phòng cụ thể (ví dụ sau khi tìm kiếm bằng SemanticSearchRooms).")]
        public async Task<string> ShowRoomCards(
            [Description("Danh sách ID phòng cần hiển thị (ví dụ: 1, 2, 3)")] string roomIds)
        {
            _logs.AppendLine($"[ShowRoomCards] roomIds={roomIds}");
            var ids = roomIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                             .Select(s => int.TryParse(s, out var id) ? id : 0)
                             .Where(id => id > 0)
                             .ToList();
            if (!ids.Any()) return "Không có phòng nào hợp lệ để hiển thị.";

            var rooms = await _context.Rooms
                .Include(r => r.Branch)
                .Where(r => ids.Contains(r.Id) && r.Status == "Available")
                .ToListAsync();

            var roomData = rooms.Select(r => new
            {
                roomId = r.Id,
                name = r.Name,
                description = r.Description,
                pricePerHour = r.PricePerHour,
                pricePerDay = r.PricePerDay,
                capacity = r.Capacity,
                maxGuests = r.MaxGuests,
                extraGuestFee = r.ExtraGuestFee,
                detailsUrl = $"/Rooms/Details/{r.Id}"
            }).ToList();

            _bookingAction = "showRooms";
            _uiBlocks.Add(new { type = "roomCards", data = new { rooms = roomData } });

            return $"Đã hiển thị thẻ phòng cho các phòng: {string.Join(", ", rooms.Select(r => r.Name))}.";
        }

        [KernelFunction("CheckHourlySlots")]
        [Description("Tìm kiếm các khung giờ trống của một phòng cụ thể trong ngày. HÃY GỌI HÀM NÀY NẾU KHÁCH HỎI THUÊ THEO GIỜ THAY VÌ TỰ BỊA RA KHUNG GIỜ.")]
        public async Task<string> CheckHourlySlots(
            [Description("ID của phòng")] int? roomId = null,
            [Description("Ngày thuê theo giờ (YYYY-MM-DD)")] string? date = null,
            [Description("Số lượng khách")] int guests = 1)
        {
            _logs.AppendLine($"[CheckHourlySlots] roomId={roomId}, date={date}, guests={guests}");
            
            if (roomId == null || roomId <= 0)
            {
                return "Thiếu ID phòng. Vui lòng chọn một phòng cụ thể.";
            }

            if (string.IsNullOrWhiteSpace(date) || !DateOnly.TryParse(date, out var slotDate))
            {
                return "Ngày thuê không hợp lệ hoặc chưa được cung cấp.";
            }

            var room = await _context.Rooms.FindAsync(roomId.Value);
            if (room == null) return "Không tìm thấy phòng.";

            var slots = await _context.RoomSlotInventories
                .Where(s => s.RoomId == roomId.Value && s.SlotDate == slotDate && s.Status == "Available")
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            var availableSlots = new List<object>();
            foreach(var slot in slots)
            {
                if (await _availabilityService.IsHourlySlotAvailableForRoomAsync(slot.Id, roomId.Value, guests))
                {
                    availableSlots.Add(new {
                        roomId = roomId,
                        roomName = room.Name,
                        slotId = slot.Id,
                        label = $"{slot.StartTime:HH:mm} - {slot.EndTime:HH:mm}",
                        totalPrice = room.PricePerHour
                    });
                }
            }

            if (!availableSlots.Any())
            {
                return $"Không có khung giờ nào trống cho {room.Name} vào ngày {date}.";
            }

            _bookingAction = "showSlots";
            _uiBlocks.Add(new { type = "hourlySlots", data = new { slots = availableSlots } });

            var resultStr = $"Tìm thấy {availableSlots.Count} khung giờ trống: " + JsonSerializer.Serialize(availableSlots);
            _logs.AppendLine($"[Result CheckHourlySlots] {resultStr}");
            return resultStr;
        }

        [KernelFunction("GenerateCheckoutLink")]
        [Description("Tạo link xem chi tiết và chốt phòng trực tiếp.")]
        public async Task<string> GenerateCheckoutLink(
            [Description("ID của phòng khách chọn")] int roomId,
            [Description("Ngày bắt đầu (YYYY-MM-DD)")] string checkInDate,
            [Description("Ngày kết thúc (YYYY-MM-DD), nếu có")] string? checkOutDate = null)
        {
            _logs.AppendLine($"[GenerateCheckoutLink] roomId={roomId}, checkInDate={checkInDate}, checkOutDate={checkOutDate}");
            
            var room = await _context.Rooms.Include(r => r.Branch).FirstOrDefaultAsync(r => r.Id == roomId);
            if (room == null) return "Không tìm thấy phòng.";

            _bookingAction = "showForm";
            string checkoutUrl = string.IsNullOrWhiteSpace(checkOutDate)
                ? $"/Bookings/CheckoutHourly?roomId={roomId}&hourlyDate={checkInDate}"
                : $"/Bookings/CheckoutDaily?roomId={roomId}&checkInDate={checkInDate}&checkOutDate={checkOutDate}";

            _uiBlocks.Add(new
            {
                type = "checkoutLink",
                data = new {
                    url = checkoutUrl,
                    title = $"Đặt ngay {room.Name}",
                    roomName = room.Name,
                    branchName = room.Branch?.Name ?? string.Empty
                }
            });

            return $"Đã tạo link checkout thành công. Yêu cầu hệ thống hiển thị nút đặt phòng. KHÔNG BAO GIỜ HIỂN THỊ LINK URL THÔ TRONG CÂU TRẢ LỜI CỦA BẠN.";
        }

        public string GetLogs() => _logs.ToString();
        public string? GetAction() => _bookingAction;
        public List<object> GetUiBlocks() => _uiBlocks;
    }
}
