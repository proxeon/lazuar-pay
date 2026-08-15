namespace Modules.One.Infrastructure.Configuration;

/// <summary>
/// Optional Development-only merchant workspace seed. Bound from
/// <c>DemoTenant</c> in <c>appsettings.Development.json</c>.
/// </summary>
public sealed class DemoTenantSettings
{
    public const string SectionName = "DemoTenant";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
}
