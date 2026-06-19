using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using WebHomestay.Services.AI;
using Xunit;
using Microsoft.Extensions.Caching.Memory;

namespace WebHomestay.Tests.Services;

public class BulkImportServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ValidateZipSizeAsync_ValidFileUnderLimit_Passes()
    {
        var context = CreateContext();
        
        // Seed database setting
        context.SystemSettings.Add(new SystemSetting
        {
            SettingKey = "MaxZipUploadSizeMB",
            SettingValue = "10",
            GroupName = "System"
        });
        await context.SaveChangesAsync();

        var env = Mock.Of<IWebHostEnvironment>();
        var embed = Mock.Of<IEmbeddingService>();
        var cache = Mock.Of<IMemoryCache>();
        var service = new BulkImportService(context, env, embed, cache);

        // File under 10MB
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(5 * 1024 * 1024); // 5MB

        // Should not throw any exception
        var exception = await Record.ExceptionAsync(() => service.ValidateZipSizeAsync(fileMock.Object));
        Assert.Null(exception);
    }

    [Fact]
    public async Task ValidateZipSizeAsync_FileOverLimit_ThrowsException()
    {
        var context = CreateContext();
        
        // Seed database setting
        context.SystemSettings.Add(new SystemSetting
        {
            SettingKey = "MaxZipUploadSizeMB",
            SettingValue = "10",
            GroupName = "System"
        });
        await context.SaveChangesAsync();

        var env = Mock.Of<IWebHostEnvironment>();
        var embed = Mock.Of<IEmbeddingService>();
        var cache = Mock.Of<IMemoryCache>();
        var service = new BulkImportService(context, env, embed, cache);

        // File over 10MB
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(11 * 1024 * 1024); // 11MB

        // Should throw exception
        var exception = await Record.ExceptionAsync(() => service.ValidateZipSizeAsync(fileMock.Object));
        Assert.NotNull(exception);
        Assert.Contains("Kích thước file vượt quá giới hạn tối đa", exception.Message);
    }

    [Fact]
    public async Task ImportBranchesAsync_SetsLeadTimeValueAndUnit()
    {
        var context = CreateContext();
        var env = Mock.Of<IWebHostEnvironment>();
        var embed = Mock.Of<IEmbeddingService>();
        var cache = Mock.Of<IMemoryCache>();
        var service = new BulkImportService(context, env, embed, cache);

        // Build a mock Excel workbook in memory
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Branches");
        worksheet.Cell(1, 1).Value = "Tên chi nhánh";
        worksheet.Cell(1, 2).Value = "Địa chỉ";
        worksheet.Cell(1, 3).Value = "Mô tả";
        worksheet.Cell(1, 4).Value = "Hotline";
        worksheet.Cell(1, 5).Value = "Email";
        worksheet.Cell(1, 6).Value = "MapUrl";
        worksheet.Cell(1, 7).Value = "LeadTime";

        worksheet.Cell(2, 1).Value = "Lumi Riverside Import Test";
        worksheet.Cell(2, 2).Value = "Q7, HCM";
        worksheet.Cell(2, 3).Value = "Mô tả ở đây";
        worksheet.Cell(2, 4).Value = "0909000999";
        worksheet.Cell(2, 5).Value = "import@lumi.vn";
        worksheet.Cell(2, 6).Value = "https://map.google.com";
        worksheet.Cell(2, 7).Value = 4; // 4 hours lead time

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("branches.xlsx");
        fileMock.Setup(f => f.Length).Returns(stream.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<System.Threading.CancellationToken>()))
            .Callback<Stream, System.Threading.CancellationToken>((s, c) => {
                stream.Position = 0;
                stream.CopyTo(s);
            })
            .Returns(Task.CompletedTask);

        var result = await service.ImportBranchesAsync(fileMock.Object);

        Assert.Equal(0, result.FailureCount);
        Assert.Equal(1, result.SuccessCount);

        var imported = await context.Branches.FirstOrDefaultAsync(b => b.Name == "Lumi Riverside Import Test");
        Assert.NotNull(imported);
        Assert.Equal(4, imported.BookingLeadTimeHours);
        Assert.Equal(4, imported.BookingLeadTimeValue);
        Assert.Equal(BranchLeadTimeUnit.Hours, imported.BookingLeadTimeUnit);
    }

    [Fact]
    public async Task ImportBranchesAsync_WithDocxFile_ParsesCorrectly()
    {
        var context = CreateContext();
        var env = Mock.Of<IWebHostEnvironment>();
        var embed = Mock.Of<IEmbeddingService>();
        var cache = Mock.Of<IMemoryCache>();
        var service = new BulkImportService(context, env, embed, cache);

        string docxPath = Path.Combine(AppContext.BaseDirectory, "../../../../docs/demo-import-word/01-mau-chi-nhanh.docx");
        if (!File.Exists(docxPath))
        {
            docxPath = @"c:\Users\admin\Documents\VS Tím\web_homestay\web_homestay\docs\demo-import-word\01-mau-chi-nhanh.docx";
        }

        Assert.True(File.Exists(docxPath), $"Docx file not found at: {docxPath}");

        using var stream = File.OpenRead(docxPath);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("01-mau-chi-nhanh.docx");
        fileMock.Setup(f => f.Length).Returns(stream.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<System.Threading.CancellationToken>()))
            .Callback<Stream, System.Threading.CancellationToken>((s, c) => {
                stream.Position = 0;
                stream.CopyTo(s);
            })
            .Returns(Task.CompletedTask);

        var result = await service.ImportBranchesAsync(fileMock.Object);

        Assert.Equal(0, result.FailureCount);
        Assert.True(result.SuccessCount > 0, "Should successfully import at least one branch from docx");

        var imported = await context.Branches.FirstOrDefaultAsync(b => b.Name == "LumiStay Phú Nhuận");
        Assert.NotNull(imported);
        Assert.Equal("18 Hoa Sứ, Phú Nhuận, TP.HCM", imported.Address);
        Assert.Equal(2, imported.BookingLeadTimeHours);
    }

    [Fact]
    public async Task ImportRoomsAsync_WithZipContainingDocx_ParsesCorrectly()
    {
        var context = CreateContext();
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        var embed = Mock.Of<IEmbeddingService>();
        var cache = Mock.Of<IMemoryCache>();
        var service = new BulkImportService(context, env.Object, embed, cache);

        context.Branches.Add(new Branch { Name = "LumiStay Phú Nhuận", Address = "18 Hoa Sứ", IsDeleted = false });
        context.Branches.Add(new Branch { Name = "LumiStay Gò Vấp", Address = "125 Quang Trung", IsDeleted = false });
        context.Branches.Add(new Branch { Name = "LumiStay Thủ Đức", Address = "9 Đặng Văn Bi", IsDeleted = false });
        await context.SaveChangesAsync();

        string zipPath = @"c:\Users\admin\Documents\VS Tím\web_homestay\web_homestay\docs\demo-import-word\02-mau-phong-ngu.zip";
        Assert.True(File.Exists(zipPath), $"Zip file not found at: {zipPath}");

        using var stream = File.OpenRead(zipPath);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("02-mau-phong-ngu.zip");
        fileMock.Setup(f => f.Length).Returns(stream.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<System.Threading.CancellationToken>()))
            .Callback<Stream, System.Threading.CancellationToken>((s, c) => {
                stream.Position = 0;
                stream.CopyTo(s);
            })
            .Returns(Task.CompletedTask);

        var result = await service.ImportRoomsAsync(fileMock.Object);

        Assert.Equal(0, result.FailureCount);
        Assert.True(result.SuccessCount > 0, "Should successfully import at least one room from zip");

        var imported = await context.Rooms.FirstOrDefaultAsync(r => r.Name == "Lụa Mây PN-201");
        Assert.NotNull(imported);
        Assert.Equal(160000, imported.PricePerHour);
        Assert.Equal(1100000, imported.PricePerDay);
    }
}
