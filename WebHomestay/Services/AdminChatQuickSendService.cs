using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;

namespace WebHomestay.Services;

public class AdminChatQuickSendService : IAdminChatQuickSendService
{
    private readonly ApplicationDbContext _context;
    private readonly IAvailabilityService _availabilityService;
    private readonly ISettingService _settingService;

    public AdminChatQuickSendService(
        ApplicationDbContext context,
        IAvailabilityService availabilityService,
        ISettingService settingService)
    {
        _context = context;
        _availabilityService = availabilityService;
        _settingService = settingService;
    }

    public async Task<AdminChatQuickSendSchema?> GetSchemaAsync(string type, CancellationToken cancellationToken = default)
    {
        var branches = await LoadBranchOptionsAsync(cancellationToken);
        var rooms = type == "slotPicker"
            ? await LoadRoomOptionsAsync(cancellationToken)
            : Array.Empty<object>();

        return type switch
        {
            "roomSelector" => new AdminChatQuickSendSchema
            {
                Type = type,
                Title = "Điều kiện gửi danh sách phòng",
                Fields =
                [
                    new
                    {
                        key = "branchId",
                        label = "Chi nhánh",
                        type = "select",
                        required = true,
                        options = branches
                    },
                    new
                    {
                        key = "bookingMode",
                        label = "Hình thức đặt",
                        type = "select",
                        required = true,
                        options = new[]
                        {
                            new { value = "hourly", label = "Theo giờ" },
                            new { value = "daily", label = "Theo ngày" }
                        }
                    },
                    new
                    {
                        key = "guestCount",
                        label = "Số khách",
                        type = "number",
                        required = true,
                        min = 1,
                        max = 20
                    },
                    new
                    {
                        key = "hourlyDate",
                        label = "Ngày đặt theo giờ",
                        type = "date",
                        required = false,
                        showWhen = new { bookingMode = "hourly" }
                    },
                    new
                    {
                        key = "checkInDate",
                        label = "Ngày nhận phòng",
                        type = "date",
                        required = false,
                        showWhen = new { bookingMode = "daily" }
                    },
                    new
                    {
                        key = "checkOutDate",
                        label = "Ngày trả phòng",
                        type = "date",
                        required = false,
                        showWhen = new { bookingMode = "daily" }
                    }
                ]
            },
            "slotPicker" => new AdminChatQuickSendSchema
            {
                Type = type,
                Title = "Điều kiện gửi khung giờ",
                Fields =
                [
                    new
                    {
                        key = "branchId",
                        label = "Chi nhánh",
                        type = "select",
                        required = true,
                        options = branches
                    },
                    new
                    {
                        key = "roomId",
                        label = "Phòng",
                        type = "select",
                        required = true,
                        options = rooms
                    },
                    new
                    {
                        key = "guestCount",
                        label = "Số khách",
                        type = "number",
                        required = true,
                        min = 1,
                        max = 20
                    },
                    new
                    {
                        key = "hourlyDate",
                        label = "Ngày cần xem khung giờ",
                        type = "date",
                        required = true
                    }
                ]
            },
            "infoForm" => new AdminChatQuickSendSchema
            {
                Type = type,
                Title = "Gửi form thông tin",
                Fields =
                [
                    new
                    {
                        key = "introMessage",
                        label = "Lời nhắn mở đầu",
                        type = "text",
                        required = false,
                        placeholder = "Ví dụ: Bạn điền thông tin giúp mình nhé."
                    }
                ]
            },
            "bookingCta" => new AdminChatQuickSendSchema
            {
                Type = type,
                Title = "Gửi CTA đặt phòng",
                Fields =
                [
                    new
                    {
                        key = "introMessage",
                        label = "Lời nhắn mở đầu",
                        type = "text",
                        required = false,
                        placeholder = "Ví dụ: Mình gửi bạn nút qua trang đặt phòng chính thức nhé."
                    }
                ]
            },
            "handoffContact" => new AdminChatQuickSendSchema
            {
                Type = type,
                Title = "Gửi liên hệ chi nhánh",
                Fields =
                [
                    new
                    {
                        key = "branchId",
                        label = "Chi nhánh",
                        type = "select",
                        required = true,
                        options = branches
                    }
                ]
            },
            _ => null
        };
    }

    public async Task<AdminChatQuickSendBuildResult?> BuildAsync(string sessionId, string type, JsonElement payload, CancellationToken cancellationToken = default)
    {
        return type switch
        {
            "roomSelector" => await BuildRoomSelectorAsync(payload, cancellationToken),
            "slotPicker" => await BuildSlotPickerAsync(payload, cancellationToken),
            "infoForm" => await BuildInfoFormAsync(payload, cancellationToken),
            "bookingCta" => await BuildBookingCtaAsync(sessionId, payload, cancellationToken),
            "handoffContact" => await BuildHandoffContactAsync(payload, cancellationToken),
            _ => null
        };
    }

    private async Task<AdminChatQuickSendBuildResult> BuildRoomSelectorAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var branchId = GetRequiredInt(payload, "branchId", "Vui lòng chọn chi nhánh.");
        var guestCount = GetRequiredInt(payload, "guestCount", "Vui lòng nhập số khách.");
        var bookingMode = GetRequiredString(payload, "bookingMode", "Vui lòng chọn hình thức đặt.");

        if (!string.Equals(bookingMode, "hourly", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(bookingMode, "daily", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Hình thức đặt chỉ hỗ trợ theo giờ hoặc theo ngày.");
        }

        if (guestCount < 1)
        {
            throw new InvalidOperationException("Số khách phải lớn hơn 0.");
        }

        IReadOnlyList<object> rooms = string.Equals(bookingMode, "daily", StringComparison.OrdinalIgnoreCase)
            ? await LoadDailyRoomsAsync(branchId, guestCount, payload, cancellationToken)
            : await LoadHourlyRoomsAsync(branchId, guestCount, payload, cancellationToken);

        return new AdminChatQuickSendBuildResult
        {
            Message = rooms.Count > 0
                ? "Mình gửi bạn danh sách phòng phù hợp theo nhu cầu hiện tại nhé."
                : "Hiện mình chưa thấy phòng phù hợp đúng điều kiện vừa chọn. Bạn đổi ngày, chi nhánh hoặc số khách giúp mình nhé.",
            UiBlocks =
            [
                new { type = "roomCards", data = new { rooms } }
            ]
        };
    }

    private async Task<AdminChatQuickSendBuildResult> BuildSlotPickerAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var branchId = GetRequiredInt(payload, "branchId", "Vui lòng chọn chi nhánh.");
        var roomId = GetRequiredInt(payload, "roomId", "Vui lòng chọn phòng.");
        var guestCount = GetRequiredInt(payload, "guestCount", "Vui lòng nhập số khách.");
        var hourlyDate = GetRequiredDate(payload, "hourlyDate", "Vui lòng chọn ngày cần xem khung giờ.");

        var room = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId && r.BranchId == branchId && r.Status == "Available", cancellationToken);

        if (room == null)
        {
            throw new InvalidOperationException("Phòng không thuộc chi nhánh đã chọn hoặc không còn khả dụng.");
        }

        if (room.MaxGuests < guestCount)
        {
            throw new InvalidOperationException("Số khách vượt quá sức chứa tối đa của phòng.");
        }

        var cutoff = await ResolveLeadTimeCutoffAsync(branchId, cancellationToken);
        var slots = await _context.RoomSlotInventories
            .AsNoTracking()
            .Include(s => s.Room)
            .Where(s => s.RoomId == roomId
                && s.SlotDate == hourlyDate
                && s.Status == "Available"
                && s.StartTime >= cutoff)
            .OrderBy(s => s.StartTime)
            .Take(24)
            .ToListAsync(cancellationToken);

        var availableSlots = new List<object>();
        foreach (var slot in slots)
        {
            if (!await _availabilityService.IsRoomAvailable(roomId, slot.StartTime, slot.EndTime))
            {
                continue;
            }

            availableSlots.Add(new
            {
                slotId = slot.Id,
                roomId = slot.RoomId,
                roomName = slot.Room?.Name ?? room.Name,
                label = slot.SlotLabel,
                startTime = slot.StartTime,
                endTime = slot.EndTime,
                totalPrice = room.PricePerHour > 0
                    ? Math.Round((decimal)(slot.EndTime - slot.StartTime).TotalHours * room.PricePerHour, 0)
                    : 0m
            });
        }

        return new AdminChatQuickSendBuildResult
        {
            Message = availableSlots.Count > 0
                ? "Mình gửi bạn các khung giờ còn trống của phòng này nhé."
                : "Hiện khung giờ phù hợp đã qua hoặc không còn trống. Bạn chọn ngày khác giúp mình nhé.",
            UiBlocks =
            [
                new { type = "hourlySlots", data = new { slots = availableSlots } }
            ]
        };
    }

    private async Task<AdminChatQuickSendBuildResult> BuildInfoFormAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var introMessage = GetOptionalString(payload, "introMessage")
            ?? "Bạn điền thông tin đặt phòng giúp mình nhé.";
        var fields = await LoadBookingFormFieldsAsync(cancellationToken);

        return new AdminChatQuickSendBuildResult
        {
            Message = introMessage,
            UiBlocks =
            [
                new { type = "bookingForm", data = new { fields } }
            ]
        };
    }

    private async Task<AdminChatQuickSendBuildResult> BuildBookingCtaAsync(string sessionId, JsonElement payload, CancellationToken cancellationToken)
    {
        var introMessage = GetOptionalString(payload, "introMessage");
        var bookingId = await FindLatestPendingBookingIdAsync(sessionId, cancellationToken);
        var targetUrl = bookingId.HasValue ? $"/Bookings/Success/{bookingId.Value}" : "/Bookings";

        return new AdminChatQuickSendBuildResult
        {
            Message = introMessage
                ?? (bookingId.HasValue
                    ? "Mình gửi bạn nút đi tới trang thanh toán/hoàn tất đặt phòng nhé."
                    : "Mình gửi bạn nút qua luồng đặt phòng chính thức nhé."),
            UiBlocks =
            [
                new
                {
                    type = "bookingCta",
                    data = new
                    {
                        target = targetUrl,
                        message = "Bạn bấm nút bên dưới để tiếp tục trên trang đặt phòng chính thức."
                    }
                }
            ]
        };
    }

    private async Task<AdminChatQuickSendBuildResult> BuildHandoffContactAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var branchId = GetRequiredInt(payload, "branchId", "Vui lòng chọn chi nhánh.");
        var branch = await _context.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);

        if (branch == null)
        {
            throw new InvalidOperationException("Không tìm thấy chi nhánh đã chọn.");
        }

        return new AdminChatQuickSendBuildResult
        {
            Message = $"Mình gửi bạn thông tin liên hệ của {branch.Name} để hỗ trợ trực tiếp nhé.",
            UiBlocks =
            [
                new
                {
                    type = "handoffContact",
                    data = new
                    {
                        message = $"Bạn liên hệ trực tiếp với {branch.Name} qua hotline hoặc email bên dưới nhé.",
                        branch = new
                        {
                            id = branch.Id,
                            name = branch.Name,
                            address = branch.Address,
                            hotline = branch.Hotline,
                            email = branch.Email
                        }
                    }
                }
            ]
        };
    }

    private async Task<List<object>> LoadHourlyRoomsAsync(int branchId, int guestCount, JsonElement payload, CancellationToken cancellationToken)
    {
        var hourlyDate = GetRequiredDate(payload, "hourlyDate", "Vui lòng chọn ngày đặt theo giờ.");
        var cutoff = await ResolveLeadTimeCutoffAsync(branchId, cancellationToken);

        var rooms = await _context.Rooms
            .AsNoTracking()
            .Include(r => r.Amenities)
            .Where(r => r.BranchId == branchId
                && r.Status == "Available"
                && r.MaxGuests >= guestCount
                && _context.RoomSlotInventories.Any(s => s.RoomId == r.Id
                    && s.SlotDate == hourlyDate
                    && s.Status == "Available"
                    && s.StartTime >= cutoff))
            .OrderBy(r => r.Name)
            .Take(12)
            .ToListAsync(cancellationToken);

        return rooms
            .Select(room => BuildRoomCard(room, guestCount, hourlyDate))
            .Cast<object>()
            .ToList();
    }

    private async Task<List<object>> LoadDailyRoomsAsync(int branchId, int guestCount, JsonElement payload, CancellationToken cancellationToken)
    {
        var checkInDate = GetRequiredDate(payload, "checkInDate", "Vui lòng chọn ngày nhận phòng.");
        var checkOutDate = GetRequiredDate(payload, "checkOutDate", "Vui lòng chọn ngày trả phòng.");

        if (checkOutDate <= checkInDate)
        {
            throw new InvalidOperationException("Ngày trả phòng phải sau ngày nhận phòng.");
        }

        var interval = BookingTimeRules.BuildDailyStay(checkInDate, checkOutDate);
        var roomIds = await _availabilityService.GetAvailableRoomIds(branchId, interval.Start, interval.End);

        var rooms = await _context.Rooms
            .AsNoTracking()
            .Include(r => r.Amenities)
            .Where(r => roomIds.Contains(r.Id)
                && r.Status == "Available"
                && r.BranchId == branchId
                && r.MaxGuests >= guestCount)
            .OrderBy(r => r.Name)
            .Take(12)
            .ToListAsync(cancellationToken);

        return rooms
            .Select(room => BuildRoomCard(room, guestCount, null))
            .Cast<object>()
            .ToList();
    }

    private object BuildRoomCard(Models.Room room, int guestCount, DateOnly? hourlyDate)
    {
        return new
        {
            roomId = room.Id,
            name = room.Name,
            description = room.Description,
            pricePerHour = room.PricePerHour,
            pricePerDay = room.PricePerDay,
            capacity = room.Capacity,
            maxGuests = room.MaxGuests,
            extraGuestFee = room.ExtraGuestFee,
            extraGuestFeeApplied = guestCount > room.Capacity ? room.ExtraGuestFee : 0m,
            fitsStandardOccupancy = guestCount <= room.Capacity,
            imageUrl = room.ImageUrl,
            detailsUrl = hourlyDate.HasValue
                ? $"/Rooms/Details/{room.Id}?hourlyDate={hourlyDate:yyyy-MM-dd}"
                : $"/Rooms/Details/{room.Id}",
            amenities = room.Amenities.Select(a => a.Name).ToList()
        };
    }

    private async Task<IReadOnlyList<object>> LoadBranchOptionsAsync(CancellationToken cancellationToken)
    {
        return await _context.Branches
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => (object)new
            {
                value = b.Id.ToString(),
                label = b.Name
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<object>> LoadRoomOptionsAsync(CancellationToken cancellationToken)
    {
        return await _context.Rooms
            .AsNoTracking()
            .Where(r => r.Status == "Available")
            .OrderBy(r => r.BranchId)
            .ThenBy(r => r.Name)
            .Select(r => (object)new
            {
                value = r.Id.ToString(),
                label = r.Name,
                branchId = r.BranchId.ToString(),
                maxGuests = r.MaxGuests
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<object>> LoadBookingFormFieldsAsync(CancellationToken cancellationToken)
    {
        var schemaJson = await _context.SystemSettings
            .Where(s => s.GroupName == "AI" && s.SettingKey == "AIBookingFormSchema")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(schemaJson) || schemaJson == "[]")
        {
            return new List<object>();
        }

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var fields = new List<object>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var id = element.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var name = element.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : id;
                var type = element.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                var label = element.TryGetProperty("label", out var labelEl) ? labelEl.GetString() : null;
                var required = element.TryGetProperty("required", out var reqEl) && reqEl.ValueKind == JsonValueKind.True;
                var helpText = element.TryGetProperty("helpText", out var helpEl) ? helpEl.GetString() : null;

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                fields.Add(new
                {
                    name,
                    type = type == "image" ? "file" : (type ?? "text"),
                    label = label ?? name,
                    required,
                    value = (string?)null,
                    placeholder = helpText ?? string.Empty
                });
            }

            return fields;
        }
        catch
        {
            return new List<object>();
        }
    }

    private async Task<int?> FindLatestPendingBookingIdAsync(string sessionId, CancellationToken cancellationToken)
    {
        var customerName = await _context.AdminChatSessions
            .Where(s => s.SessionId == sessionId)
            .Select(s => s.CustomerName)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(customerName))
        {
            return null;
        }

        return await _context.Bookings
            .Where(b => b.CustomerName == customerName
                && (b.Status == "PendingPayment" || b.Status == "AwaitingPayment"))
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<DateTime> ResolveLeadTimeCutoffAsync(int branchId, CancellationToken cancellationToken)
    {
        var globalLeadTime = await _settingService.GetIntAsync("BookingLeadTimeHours", 2);
        var branchLeadTime = await _context.Branches
            .AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => (int?)b.BookingLeadTimeHours)
            .FirstOrDefaultAsync(cancellationToken);

        var leadTimeHours = branchLeadTime is > 0
            ? branchLeadTime.GetValueOrDefault()
            : globalLeadTime;

        return DateTime.Now.AddHours(Math.Max(0, leadTimeHours));
    }

    private static int GetRequiredInt(JsonElement payload, string key, string errorMessage)
    {
        if (!payload.TryGetProperty(key, out var element))
        {
            throw new InvalidOperationException(errorMessage);
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            return number;
        }

        if (element.ValueKind == JsonValueKind.String
            && int.TryParse(element.GetString(), out number))
        {
            return number;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static string GetRequiredString(JsonElement payload, string key, string errorMessage)
    {
        var value = GetOptionalString(payload, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value;
    }

    private static string? GetOptionalString(JsonElement payload, string key)
    {
        if (!payload.TryGetProperty(key, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString()?.Trim() : null;
    }

    private static DateOnly GetRequiredDate(JsonElement payload, string key, string errorMessage)
    {
        var value = GetRequiredString(payload, key, errorMessage);
        if (!DateOnly.TryParse(value, out var date))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return date;
    }
}
