using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;

namespace WebHomestay.Services;

public class PublicBookingRoomExplanationService : IPublicBookingRoomExplanationService
{
    private readonly ApplicationDbContext _context;

    public PublicBookingRoomExplanationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PublicBookingRoomExplanation> BuildAsync(int roomId, BookingConfirmedState state, CancellationToken cancellationToken)
    {
        var room = await _context.Rooms.FirstAsync(r => r.Id == roomId, cancellationToken);
        var guestCount = Math.Max(state.GuestCount, 1);
        var extraGuestCount = Math.Max(0, guestCount - room.Capacity);
        var allowsRequestedGuests = guestCount <= room.MaxGuests;
        var pricingTier = await ResolvePricingTierAsync(state, cancellationToken);
        var displayPrice = ResolveDisplayPrice(room, state.BookingMode, pricingTier);

        var lines = new List<string>
        {
            $"Tiêu chuẩn {room.Capacity} khách, tối đa {room.MaxGuests} khách."
        };

        if (extraGuestCount > 0 && allowsRequestedGuests)
        {
            lines.Add($"Vượt chuẩn {extraGuestCount} khách, phụ thu {room.ExtraGuestFee:N0}đ/khách.");
        }

        lines.Add(pricingTier switch
        {
            "holiday" => "Thời gian này áp dụng giá ngày lễ.",
            "weekend" => "Thời gian này áp dụng giá cuối tuần.",
            _ => "Thời gian này áp dụng giá ngày thường."
        });

        return new PublicBookingRoomExplanation
        {
            FitsStandardOccupancy = guestCount <= room.Capacity,
            AllowsRequestedGuests = allowsRequestedGuests,
            ExtraGuestCount = extraGuestCount,
            ExtraGuestFeeApplied = extraGuestCount > 0 ? room.ExtraGuestFee : 0m,
            PricingTierLabel = pricingTier,
            Lines = lines,
            RecommendationReason = allowsRequestedGuests
                ? "Phù hợp số khách và dữ liệu giá hiện tại."
                : "Vượt số khách tối đa của phòng.",
            DisplayPrice = displayPrice
        };
    }

    private async Task<string> ResolvePricingTierAsync(BookingConfirmedState state, CancellationToken cancellationToken)
    {
        var dates = ExpandRelevantDates(state);
        if (dates.Count == 0)
        {
            return "weekday";
        }

        var holidays = await _context.Holidays
            .AsNoTracking()
            .Where(h => dates.Contains(DateOnly.FromDateTime(h.Date)))
            .Select(h => DateOnly.FromDateTime(h.Date))
            .ToListAsync(cancellationToken);

        if (holidays.Count > 0)
        {
            return "holiday";
        }

        return dates.Any(IsWeekend) ? "weekend" : "weekday";
    }

    private static List<DateOnly> ExpandRelevantDates(BookingConfirmedState state)
    {
        if (state.CheckInDate.HasValue && state.CheckOutDate.HasValue)
        {
            var dates = new List<DateOnly>();
            for (var current = state.CheckInDate.Value; current < state.CheckOutDate.Value; current = current.AddDays(1))
            {
                dates.Add(current);
            }

            if (dates.Count == 0)
            {
                dates.Add(state.CheckInDate.Value);
            }

            return dates;
        }

        if (state.HourlyDate.HasValue)
        {
            return [state.HourlyDate.Value];
        }

        if (state.CheckInDate.HasValue)
        {
            return [state.CheckInDate.Value];
        }

        return [];
    }

    private static bool IsWeekend(DateOnly date)
    {
        var dayOfWeek = date.DayOfWeek;
        return dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    private static decimal ResolveDisplayPrice(Models.Room room, string bookingMode, string pricingTier)
    {
        var isDaily = string.Equals(bookingMode, "daily", StringComparison.OrdinalIgnoreCase);
        return (pricingTier, isDaily) switch
        {
            ("holiday", true) when room.PriceHolidayPerDay > 0 => room.PriceHolidayPerDay,
            ("holiday", false) when room.PriceHolidayPerHour > 0 => room.PriceHolidayPerHour,
            ("weekend", true) when room.PriceWeekendPerDay > 0 => room.PriceWeekendPerDay,
            ("weekend", false) when room.PriceWeekendPerHour > 0 => room.PriceWeekendPerHour,
            (_, true) => room.PricePerDay,
            _ => room.PricePerHour
        };
    }
}
