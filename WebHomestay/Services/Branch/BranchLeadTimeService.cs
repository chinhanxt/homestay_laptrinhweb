using WebHomestay.Services;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public class BranchLeadTimeService : IBranchLeadTimeService
{
    private readonly ApplicationDbContext _context;
    private readonly Func<DateTime> _utcNow;
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public BranchLeadTimeService(ApplicationDbContext context, Func<DateTime>? utcNow = null)
    {
        _context = context;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<BranchLeadTimeRule> ResolveAsync(int? branchId, CancellationToken cancellationToken = default)
    {
        var branch = branchId.HasValue
            ? await _context.Branches
                .AsNoTracking()
                .Where(b => b.Id == branchId.Value)
                .Select(b => new { b.Id, b.BookingLeadTimeHours, b.BookingLeadTimeDays, b.BookingLeadTimeValue, b.BookingLeadTimeUnit })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var hourlyHours = branch?.BookingLeadTimeHours ?? 2;
        var dailyDays = branch?.BookingLeadTimeDays ?? 1;
        var nowUtc = _utcNow();
        var nowLocal = ConvertUtcToVietnamTime(nowUtc);
        var hourlyCutoffUtc = nowUtc.AddHours(hourlyHours);
        var hourlyCutoffLocal = ConvertUtcToVietnamTime(hourlyCutoffUtc);

        return new BranchLeadTimeRule
        {
            BranchId = branch?.Id ?? 0,
            Value = branch?.BookingLeadTimeValue ?? 2,
            Unit = BranchLeadTimeUnit.Normalize(branch?.BookingLeadTimeUnit),
            HourlyLeadTimeHours = hourlyHours,
            DailyLeadTimeDays = dailyDays,
            HourlyCutoffUtc = hourlyCutoffUtc,
            EarliestAllowedHourlyDate = DateOnly.FromDateTime(hourlyCutoffLocal),
            EarliestAllowedDailyDate = DateOnly.FromDateTime(nowLocal).AddDays(Math.Max(0, dailyDays))
        };
    }

    private static DateTime ConvertUtcToVietnamTime(DateTime utc)
    {
        var normalizedUtc = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, VietnamTimeZone);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");
        }
    }
}
