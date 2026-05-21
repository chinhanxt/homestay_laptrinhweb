using Xunit;

namespace WebHomestay.Tests.Domain;

public class PermissionInheritanceTests
{
    private static bool ValidateInheritance(Dictionary<string, bool> perms, out string errorMessage)
    {
        var parentChildMap = new Dictionary<string, string[]>
        {
            ["bookings.view"] = new[] { "bookings.detail", "bookings.create", "bookings.edit", "bookings.delete", "bookings.trash", "bookings.restore" },
            ["branches.view"] = new[] { "branches.detail", "branches.create", "branches.edit", "branches.delete" },
            ["rooms.view"] = new[] { "rooms.detail", "rooms.create", "rooms.edit", "rooms.delete" },
            ["images.view"] = new[] { "images.detail" },
            ["staff.view"] = new[] { "staff.create", "staff.edit", "staff.delete", "staff.permissions", "staff.logs" },
            ["settings.view"] = new[] { "settings.update", "holidays.manage", "slots.manage" },
            ["ai.view"] = new[] { "ai.manage" }
        };

        foreach (var kvp in parentChildMap)
        {
            var parentHas = perms.TryGetValue(kvp.Key, out var pv) && pv;
            foreach (var child in kvp.Value)
            {
                var childHas = perms.TryGetValue(child, out var cv) && cv;
                if (childHas && !parentHas)
                {
                    errorMessage = "Inheritance violation";
                    return false;
                }
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    [Fact]
    public void Valid_Permissions_Pass()
    {
        var perms = new Dictionary<string, bool>
        {
            ["rooms.view"] = true,
            ["rooms.edit"] = true,
            ["bookings.view"] = true,
            ["bookings.detail"] = true
        };
        Assert.True(ValidateInheritance(perms, out _));
    }

    [Fact]
    public void Child_Without_Parent_Fails()
    {
        var perms = new Dictionary<string, bool> { ["rooms.edit"] = true };
        Assert.False(ValidateInheritance(perms, out _));
    }

    [Fact]
    public void Parent_Alone_Is_Valid()
    {
        var perms = new Dictionary<string, bool> { ["rooms.view"] = true };
        Assert.True(ValidateInheritance(perms, out _));
    }

    [Fact]
    public void Multiple_Children_All_Need_Parent()
    {
        var perms = new Dictionary<string, bool>
        {
            ["rooms.view"] = true,
            ["rooms.detail"] = true,
            ["rooms.edit"] = true,
            ["bookings.detail"] = true // Missing bookings.view!
        };
        Assert.False(ValidateInheritance(perms, out _));
    }
}
