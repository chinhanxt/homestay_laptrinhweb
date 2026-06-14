using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.Number;

namespace WebHomestay.Services.AI
{
    public class EntityExtractorService : IEntityExtractorService
    {
        public (DateTime? Date, TimeSpan? Time) ExtractDateTime(string text)
        {
            var lowered = text.ToLowerInvariant();
            var vietnamTime = DateTime.UtcNow.AddHours(7);
            
            // Vietnamese phrase variations
            if (lowered.Contains("hôm nay") || lowered.Contains("nay") || lowered.Contains("hom nay"))
            {
                return (vietnamTime.Date, null);
            }
            if (lowered.Contains("ngày mai") || lowered.Contains("mai") || lowered.Contains("ngay mai"))
            {
                return (vietnamTime.Date.AddDays(1), null);
            }
            if (lowered.Contains("ngày mốt") || lowered.Contains("mốt") || lowered.Contains("ngay mot"))
            {
                return (vietnamTime.Date.AddDays(2), null);
            }

            // Vietnamese date regex (e.g., 20/10, ngày 20 tháng 10)
            var dateMatch = Regex.Match(lowered, @"(?:ngày\s*)?(\d{1,2})\s*(?:[/-]|tháng\s*)\s*(\d{1,2})(?:\s*(?:[/-]|năm\s*)\s*(\d{2,4}))?");
            if (dateMatch.Success)
            {
                if (int.TryParse(dateMatch.Groups[1].Value, out var day) && int.TryParse(dateMatch.Groups[2].Value, out var month))
                {
                    var year = dateMatch.Groups[3].Success ? int.Parse(dateMatch.Groups[3].Value) : vietnamTime.Year;
                    if (year < 100) year += 2000;
                    
                    try 
                    {
                        var extractedDate = new DateTime(year, month, day);
                        // Prevent booking past dates, assume next year if month already passed
                        if (extractedDate < vietnamTime.Date && !dateMatch.Groups[3].Success)
                        {
                            extractedDate = extractedDate.AddYears(1);
                        }
                        return (extractedDate.Date, null);
                    }
                    catch { } // Invalid date
                }
            }

            try
            {
                // Fallback to English for better NLP if Vietnamese is not perfectly supported
                var results = DateTimeRecognizer.RecognizeDateTime(text, Culture.English);
                
                if (results.Count > 0 && results[0].Resolution.ContainsKey("values"))
                {
                    var values = (List<Dictionary<string, string>>)results[0].Resolution["values"];
                    var value = values.FirstOrDefault()?.GetValueOrDefault("value");
                    
                    if (value != null && DateTime.TryParse(value, out var dt))
                    {
                        return (dt.Date, dt.TimeOfDay);
                    }
                }
            }
            catch
            {
                // Ignore exceptions to fallback gracefully
            }
            
            return (null, null);
        }

        public int? ExtractGuestCount(string text)
        {
            var lowered = text.ToLowerInvariant();
            
            // Vietnamese variations
            var guestMatch = Regex.Match(lowered, @"(\d+)\s*(người|ng|khách|khach|nguoi)");
            if (guestMatch.Success && int.TryParse(guestMatch.Groups[1].Value, out var count))
            {
                return count;
            }

            var partyMatch = Regex.Match(lowered, @"(?:đi|nhóm|team|party)\s*(\d+)");
            if (partyMatch.Success && int.TryParse(partyMatch.Groups[1].Value, out count))
            {
                return count;
            }

            try
            {
                if (!Regex.IsMatch(lowered, @"\b(mấy|bao nhiêu|bao nhieu)\s*(người|khách|nguoi|khach)\b"))
                {
                    return null;
                }

                var results = NumberRecognizer.RecognizeNumber(text, Culture.English);
                
                if (results.Count > 0 && results[0].Resolution.ContainsKey("value"))
                {
                    if (int.TryParse(results[0].Resolution["value"].ToString(), out var number) && number > 0 && number <= 20)
                    {
                        return number;
                    }
                }
            }
            catch
            {
                // Ignore exceptions
            }
            return null;
        }
    }
}
