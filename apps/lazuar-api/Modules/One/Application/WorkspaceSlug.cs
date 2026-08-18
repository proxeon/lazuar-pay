using System;

namespace Modules.One.Application;

public static class WorkspaceSlug
{
    public const string TakenMessage = "The requested workspace slug is already taken. Please choose another.";

    public static bool LooksLikeUniqueViolation(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("23505", StringComparison.Ordinal)
               && (text.Contains("Slug", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("Organizations_Slug", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("IX_Organizations_Slug", StringComparison.OrdinalIgnoreCase));
    }
}
