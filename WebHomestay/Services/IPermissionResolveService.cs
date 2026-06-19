using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
using Microsoft.AspNetCore.Http;

namespace WebHomestay.Services;

public interface IPermissionResolveService
{
    bool HasPermission(HttpContext httpContext, string permissionKey);
    Dictionary<string, bool> GetEffectivePermissions(HttpContext httpContext);
    void InvalidateCache(string role);
}
