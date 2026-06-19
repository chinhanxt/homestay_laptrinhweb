using WebHomestay.Services;
// WebHomestay/Services/BookingTimeRules.cs
namespace WebHomestay.Services;

public readonly record struct BookingInterval(DateTime Start, DateTime End);

public static class BookingTimeRules
{
    public static readonly TimeOnly DailyCheckIn = new(14, 0);
    public static readonly TimeOnly DailyCheckOut = new(12, 0);

    public static BookingInterval BuildDailyStay(DateOnly checkInDate, DateOnly checkOutDate)
    {
        var start = checkInDate.ToDateTime(DailyCheckIn);
        var end = checkOutDate.ToDateTime(DailyCheckOut);
        return new BookingInterval(start, end);
    }

    public static bool Overlaps(DateTime existingStart, DateTime existingEnd, DateTime candidateStart, DateTime candidateEnd)
    {
        return existingStart < candidateEnd && existingEnd > candidateStart;
    }
}
