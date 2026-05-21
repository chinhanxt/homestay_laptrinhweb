using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class PermissionResolveServiceTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();

        ctx.RolePermissionTemplates.AddRange(
            new RolePermissionTemplate
            {
                Role = "Manager",
                Permissions = new Dictionary<string, bool> { ["rooms.view"] = true, ["rooms.edit"] = true }
            },
            new RolePermissionTemplate
            {
                Role = "Staff",
                Permissions = new Dictionary<string, bool> { ["rooms.view"] = true }
            }
        );
        ctx.SaveChanges();
        return ctx;
    }

    private (PermissionResolveService, DefaultHttpContext) CreateService(ApplicationDbContext ctx, string role, Dictionary<string, bool>? overridePerms = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var scopeFactory = new FakeServiceScopeFactory(ctx);

        var httpContext = new DefaultHttpContext();
        var session = new FakeSession();
        httpContext.Session = session;
        session.SetString("AdminRole", role);

        if (overridePerms != null)
        {
            session.SetString("AdminPermissions", System.Text.Json.JsonSerializer.Serialize(overridePerms));
        }

        var service = new PermissionResolveService(cache, scopeFactory);
        return (service, httpContext);
    }

    [Fact]
    public void SuperAdmin_Always_HasPermission()
    {
        var ctx = CreateDbContext();
        var (service, http) = CreateService(ctx, "SuperAdmin");
        Assert.True(service.HasPermission(http, "anything.at.all"));
    }

    [Fact]
    public void Manager_Uses_RoleTemplate()
    {
        var ctx = CreateDbContext();
        var (service, http) = CreateService(ctx, "Manager");
        Assert.True(service.HasPermission(http, "rooms.view"));
        Assert.True(service.HasPermission(http, "rooms.edit"));
        Assert.False(service.HasPermission(http, "rooms.delete"));
    }

    [Fact]
    public void Staff_Uses_RoleTemplate()
    {
        var ctx = CreateDbContext();
        var (service, http) = CreateService(ctx, "Staff");
        Assert.True(service.HasPermission(http, "rooms.view"));
        Assert.False(service.HasPermission(http, "rooms.edit"));
    }

    [Fact]
    public void Override_Wins_Over_Template()
    {
        var ctx = CreateDbContext();
        var overrides = new Dictionary<string, bool> { ["rooms.edit"] = true };
        var (service, http) = CreateService(ctx, "Staff", overrides);
        Assert.True(service.HasPermission(http, "rooms.edit"));
    }

    [Fact]
    public void Override_Can_Remove_Template_Permission()
    {
        var ctx = CreateDbContext();
        var overrides = new Dictionary<string, bool> { ["rooms.view"] = false };
        var (service, http) = CreateService(ctx, "Manager", overrides);
        Assert.False(service.HasPermission(http, "rooms.view"));
    }
}
