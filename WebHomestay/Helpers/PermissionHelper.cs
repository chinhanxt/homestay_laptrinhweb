using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace WebHomestay.Helpers
{
    public static class PermissionHelper
    {
        public static bool HasPermission(HttpContext context, string permission)
        {
            var role = context.Session.GetString("AdminRole");
            if (role == "SuperAdmin") return true;

            var permsJson = context.Session.GetString("AdminPermissions");
            if (string.IsNullOrEmpty(permsJson)) return false;

            try
            {
                var perms = JsonSerializer.Deserialize<Dictionary<string, bool>>(permsJson);
                return perms != null && perms.ContainsKey(permission) && perms[permission];
            }
            catch
            {
                return false;
            }
        }
    }
}
