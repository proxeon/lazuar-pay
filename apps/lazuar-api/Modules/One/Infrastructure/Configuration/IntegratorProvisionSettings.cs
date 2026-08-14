namespace Modules.One.Infrastructure.Configuration;

/// <summary>
/// Shared secret for machine workspace provision (integrator → Pay).
/// Config keys: <c>INTEGRATOR_PROVISION_SECRET</c> or <c>IntegratorProvision:Secret</c>.
/// </summary>
public sealed class IntegratorProvisionSettings
{
    public const string SectionName = "IntegratorProvision";

    /// <summary>
    /// High-entropy secret (32+ bytes recommended). Empty/unset rejects provision secret auth.
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Max provision requests per minute per secret identity (default 30).</summary>
    public int RateLimitPerMinute { get; set; } = 30;

    /// <summary>Max provision requests per minute per (external_product, external_org_id) (default 10).</summary>
    public int RateLimitPerAuraOrgPerMinute { get; set; } = 10;
}
