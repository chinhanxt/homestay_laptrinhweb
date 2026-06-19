using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Models.Configuration;

namespace WebHomestay.Services;

public class PermissionResolveService : IPermissionResolveService
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    private const string CacheKeyPrefix = "role_perms_";

    public PermissionResolveService(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public bool HasPermission(HttpContext httpContext, string permissionKey)
    {
        var role = httpContext.Session.GetString("AdminRole");
        if (role == "SuperAdmin") return true;
        if (string.IsNullOrEmpty(role)) return false;

        var effective = GetEffectivePermissions(httpContext);
        return effective.TryGetValue(permissionKey, out var has) && has;
    }

    public Dictionary<string, bool> GetEffectivePermissions(HttpContext httpContext)
    {
        var role = httpContext.Session.GetString("AdminRole");
        if (role == "SuperAdmin" || string.IsNullOrEmpty(role))
            return new Dictionary<string, bool>();

        var template = GetRoleTemplate(role);

        var overrideJson = httpContext.Session.GetString("AdminPermissions");
        Dictionary<string, bool>? overrideDict = null;
        if (!string.IsNullOrEmpty(overrideJson))
        {
            try { overrideDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(overrideJson); }
            catch { }
        }

        if (overrideDict == null || overrideDict.Count == 0)
            return NormalizePermissions(template);

        var merged = new Dictionary<string, bool>(template);
        foreach (var kvp in overrideDict)
        {
            merged[kvp.Key] = kvp.Value;
        }
        return NormalizePermissions(merged);
    }

    private Dictionary<string, bool> GetRoleTemplate(string role)
    {
        var cacheKey = CacheKeyPrefix + role;
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, bool>? cached) && cached != null)
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var template = context.RolePermissionTemplates.AsNoTracking()
            .FirstOrDefault(t => t.Role == role);

        var result = NormalizePermissions(template?.Permissions ?? new Dictionary<string, bool>());

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    private static Dictionary<string, bool> NormalizePermissions(Dictionary<string, bool> source)
    {
        var normalized = new Dictionary<string, bool>(source);

        // Backward compatibility: old images.detail used as delete permission.
        if (normalized.TryGetValue("images.detail", out var canDeleteImages) && canDeleteImages)
        {
            normalized["images.delete"] = true;
        }

        return normalized;
    }

    public void InvalidateCache(string role)
    {
        _cache.Remove(CacheKeyPrefix + role);
    }
}
