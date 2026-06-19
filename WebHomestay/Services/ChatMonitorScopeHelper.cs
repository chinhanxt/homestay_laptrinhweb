using Microsoft.AspNetCore.Http;
using WebHomestay.Models;

namespace WebHomestay.Services;

public static class ChatMonitorScopeHelper
{
    public const string GlobalAdminMonitorGroup = "admin_monitor";

    public static bool IsSuperAdmin(ISession session)
        => string.Equals(session.GetString("AdminRole"), "SuperAdmin", StringComparison.Ordinal);

    public static int? GetScopedBranchId(ISession session)
        => IsSuperAdmin(session) ? null : session.GetInt32("AdminBranchId");

    public static IQueryable<AdminChatSession> ApplyBranchScope(
        IQueryable<AdminChatSession> query,
        int? branchId,
        bool isSuperAdmin)
    {
        if (isSuperAdmin)
        {
            return query;
        }

        if (!branchId.HasValue)
        {
            return query.Where(_ => false);
        }

        return query.Where(session => session.BranchId == branchId.Value);
    }

    public static List<string> GetMonitorGroupsForViewer(ISession session)
    {
        if (IsSuperAdmin(session))
        {
            return new List<string> { GlobalAdminMonitorGroup };
        }

        var branchId = session.GetInt32("AdminBranchId");
        return branchId.HasValue
            ? new List<string> { GetBranchAdminMonitorGroup(branchId.Value) }
            : new List<string>();
    }

    public static List<string> GetMonitorGroupsForSession(int? branchId)
    {
        var groups = new List<string> { GlobalAdminMonitorGroup };
        if (branchId.HasValue)
        {
            groups.Add(GetBranchAdminMonitorGroup(branchId.Value));
        }

        return groups;
    }

    public static string GetBranchAdminMonitorGroup(int branchId)
        => $"admin_monitor_branch_{branchId}";
}
