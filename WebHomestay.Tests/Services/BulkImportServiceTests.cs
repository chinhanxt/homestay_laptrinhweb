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
        var service = new BulkImportService(context, env, embed);

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
        var service = new BulkImportService(context, env, embed);

        // File over 10MB
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(11 * 1024 * 1024); // 11MB

        // Should throw exception
        var exception = await Record.ExceptionAsync(() => service.ValidateZipSizeAsync(fileMock.Object));
        Assert.NotNull(exception);
        Assert.Contains("Kích thước file vượt quá giới hạn tối đa", exception.Message);
    }
}
