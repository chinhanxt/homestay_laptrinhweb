namespace WebHomestay.Models;

public static class BranchLeadTimeUnit
{
    public const string Hours = "Hours";
    public const string Days = "Days";

    public static string Normalize(string? value)
    {
        return string.Equals(value, Days, StringComparison.OrdinalIgnoreCase)
            ? Days
            : Hours;
    }
}
