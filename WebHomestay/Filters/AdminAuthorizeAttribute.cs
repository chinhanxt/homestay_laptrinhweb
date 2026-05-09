using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

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

            // 1. Kiểm tra đăng nhập
            if (string.IsNullOrEmpty(user))
            {
                context.Result = new RedirectToActionResult("Login", "AdminAccount", null);
                return;
            }

            // SuperAdmin luôn có quyền, không cần kiểm tra thêm
            if (role == "SuperAdmin") return;

            // 2. Kiểm tra quyền chi tiết (Permission Key) - ƯU TIÊN HÀNG ĐẦU
            if (!string.IsNullOrEmpty(Permission))
            {
                var permsJson = session.GetString("AdminPermissions");
                if (!string.IsNullOrEmpty(permsJson))
                {
                    try 
                    {
                        var perms = JsonSerializer.Deserialize<Dictionary<string, bool>>(permsJson);
                        if (perms != null && perms.ContainsKey(Permission) && perms[Permission])
                        {
                            // Nếu có quyền trong ô tích, cho phép truy cập ngay
                            return;
                        }
                    }
                    catch { /* Ignore */ }
                }
            }

            // 3. Nếu không có quyền chi tiết, kiểm tra vai trò (Role)
            if (_roles.Length > 0 && !_roles.Contains(role))
            {
                context.Result = new ContentResult { 
                    Content = $"Bạn không có quyền thực hiện hành động này. (Yêu cầu: {Permission ?? string.Join("/", _roles)})", 
                    StatusCode = 403 
                };
                return;
            }
            
            // Nếu Action yêu cầu quyền chi tiết mà user không có (và không thuộc Role cho phép)
            if (!string.IsNullOrEmpty(Permission))
            {
                context.Result = new ContentResult { 
                    Content = $"Bạn không có quyền truy cập tính năng: {Permission}", 
                    StatusCode = 403 
                };
            }
        }
    }
}
