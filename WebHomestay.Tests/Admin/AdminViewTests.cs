// WebHomestay.Tests/Admin/AdminViewTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using Xunit;

namespace WebHomestay.Tests.Admin;

public class AdminViewTests
{
    [Fact]
    public void BookingModel_ContainsModeAndSlot_ForReporting()
    {
        var booking = new Booking
        {
            BookingMode = BookingMode.Hourly,
            SlotLabel = "09:00-11:00"
        };

        Assert.Equal(BookingMode.Hourly, booking.BookingMode);
        Assert.Equal("09:00-11:00", booking.SlotLabel);
    }
}
