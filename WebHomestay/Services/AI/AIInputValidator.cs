using System;
using System.Collections.Generic;

namespace WebHomestay.Services.AI;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IAIInputValidator
{
    ValidationResult ValidateBookingInput(string bookingMode, DateOnly? checkIn, DateOnly? checkOut, DateOnly? hourlyDate, int guestCount);
}

public class AIInputValidator : IAIInputValidator
{
    public ValidationResult ValidateBookingInput(string bookingMode, DateOnly? checkIn, DateOnly? checkOut, DateOnly? hourlyDate, int guestCount)
    {
        if (guestCount < 1)
        {
            return new ValidationResult { IsValid = false, ErrorMessage = "Số lượng khách phải lớn hơn 0." };
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)); // Vietnam time

        if (bookingMode == "hourly")
        {
            if (!hourlyDate.HasValue)
                return new ValidationResult { IsValid = false, ErrorMessage = "Vui lòng chọn ngày nhận phòng." };
            
            if (hourlyDate.Value < today)
                return new ValidationResult { IsValid = false, ErrorMessage = "Ngày nhận phòng không thể trong quá khứ." };
        }
        else if (bookingMode == "daily")
        {
            if (!checkIn.HasValue)
                return new ValidationResult { IsValid = false, ErrorMessage = "Vui lòng chọn ngày nhận phòng." };
            if (!checkOut.HasValue)
                return new ValidationResult { IsValid = false, ErrorMessage = "Vui lòng chọn ngày trả phòng." };
            
            if (checkIn.Value < today)
                return new ValidationResult { IsValid = false, ErrorMessage = "Ngày nhận phòng không thể trong quá khứ." };
            
            if (checkOut.Value <= checkIn.Value)
                return new ValidationResult { IsValid = false, ErrorMessage = "Ngày trả phòng phải sau ngày nhận phòng." };
        }

        return new ValidationResult { IsValid = true };
    }
}
