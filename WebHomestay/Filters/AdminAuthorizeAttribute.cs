using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebHomestay.Services;

namespace WebHomestay.Filters
{
    public class AdminAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _roles;
        public string? Permission { get; set; }

        public AdminAuthorizeAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var session = context.HttpContext.Session;
            var user = session.GetString("AdminUser");
            var role = session.GetString("AdminRole");

            // 1. Check login
            if (string.IsNullOrEmpty(user))
            {
                context.Result = new RedirectToActionResult("Login", "AdminAccount", null);
                return;
            }

            // SuperAdmin always has full access
            if (role == "SuperAdmin") return;

            // 2. Check detailed permission via PermissionResolveService
            if (!string.IsNullOrEmpty(Permission))
            {
                var resolveService = context.HttpContext.RequestServices
                    .GetRequiredService<IPermissionResolveService>();
                if (resolveService.HasPermission(context.HttpContext, Permission))
                    return;
            }

            // 3. Check role-based access
            if (_roles.Length > 0 && !_roles.Contains(role))
            {
                context.Result = new ContentResult
                {
                    Content = $"Bạn không có quyền thực hiện hành động này. (Yêu cầu: {Permission ?? string.Join("/", _roles)})",
                    StatusCode = 403
                };
                return;
            }

            // 4. No permission match
            if (!string.IsNullOrEmpty(Permission))
            {
                context.Result = new ContentResult
                {
                    Content = $"Bạn không có quyền truy cập tính năng: {Permission}",
                    StatusCode = 403
                };
            }
        }
    }
}
