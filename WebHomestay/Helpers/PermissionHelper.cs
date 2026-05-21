using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WebHomestay.Services;

namespace WebHomestay.Helpers
{
    public static class PermissionHelper
    {
        public static bool HasPermission(HttpContext context, string permission)
        {
            var role = context.Session.GetString("AdminRole");
            if (role == "SuperAdmin") return true;
            if (string.IsNullOrEmpty(role)) return false;

            var resolveService = context.RequestServices.GetRequiredService<IPermissionResolveService>();
            return resolveService.HasPermission(context, permission);
        }
    }
}
