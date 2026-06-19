using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public class BranchLeadTimeService : IBranchLeadTimeService
{
    private readonly ApplicationDbContext _context;
    private readonly Func<DateTime> _utcNow;

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

        return new BranchLeadTimeRule
        {
            BranchId = branch?.Id ?? 0,
            Value = branch?.BookingLeadTimeValue ?? 2,
            Unit = BranchLeadTimeUnit.Normalize(branch?.BookingLeadTimeUnit),
            HourlyLeadTimeHours = hourlyHours,
            DailyLeadTimeDays = dailyDays,
            HourlyCutoffUtc = nowUtc.AddHours(hourlyHours),
            EarliestAllowedDailyDate = dailyDays > 0
                ? DateOnly.FromDateTime(nowUtc.Date).AddDays(dailyDays + 1)
                : DateOnly.FromDateTime(nowUtc.Date)
        };
    }
}
