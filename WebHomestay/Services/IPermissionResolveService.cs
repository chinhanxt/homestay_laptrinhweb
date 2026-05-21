using Microsoft.AspNetCore.Http;

namespace WebHomestay.Services;

public interface IPermissionResolveService
{
    bool HasPermission(HttpContext httpContext, string permissionKey);
    Dictionary<string, bool> GetEffectivePermissions(HttpContext httpContext);
    void InvalidateCache(string role);
}
