using System;

namespace WebHomestay.Services.AI
{
    public interface IEntityExtractorService
    {
        (DateTime? Date, TimeSpan? Time) ExtractDateTime(string text);
        int? ExtractGuestCount(string text);
    }
}
