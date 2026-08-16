using System;

namespace Modules.One.Domain;

public static class WorkspaceStaffRoles
{
    public const string Admin = "ADMIN";
    public const string Member = "MEMBER";
    public const string Viewer = "VIEWER";
    public const string SuperAdmin = "SUPER_ADMIN";

    public static string NormalizeInvitedRole(string? role)
    {
        var normalized = (role ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized is not (Admin or Member or Viewer))
        {
            throw new InvalidOperationException("Role must be ADMIN, MEMBER, or VIEWER.");
        }

        return normalized;
    }

    public static bool CanManageMembers(string? role)
    {
        var normalized = (role ?? string.Empty).Trim().ToUpperInvariant();
        return normalized is Admin or SuperAdmin;
    }
}
