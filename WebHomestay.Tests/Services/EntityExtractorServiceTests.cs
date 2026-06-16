using WebHomestay.Services.AI;
using Xunit;

namespace WebHomestay.Tests.Services;

public class EntityExtractorServiceTests
{
    [Fact]
    public void ExtractGuestCount_DoesNotTreatRoomNumberAsGuestCount()
    {
        var service = new EntityExtractorService();

        var result = service.ExtractGuestCount("An Nhiên DN-101 có khung giờ trống không");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractGuestCount_StillExtractsExplicitGuestCount()
    {
        var service = new EntityExtractorService();

        var result = service.ExtractGuestCount("đi 3 người ngày 12/6");

        Assert.Equal(3, result);
    }

    [Fact]
    public void ExtractGuestCount_ExtractsTotalGuestPhrase()
    {
        var service = new EntityExtractorService();

        var result = service.ExtractGuestCount("tui muốn thêm 1 khách nữa tổng là 3");

        Assert.Equal(3, result);
    }
}
