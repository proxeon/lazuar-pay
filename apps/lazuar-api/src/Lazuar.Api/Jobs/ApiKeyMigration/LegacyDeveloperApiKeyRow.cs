namespace Lazuar.Api.Jobs.ApiKeyMigration;

/// <summary>
/// Read model for a row in <c>lhdn.DeveloperApiKeys</c> (no plaintext secret).
/// </summary>
public sealed class LegacyDeveloperApiKeyRow
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Prefix { get; init; } = string.Empty;
    public string KeyHash { get; init; } = string.Empty;
    public string KeyHint { get; init; } = string.Empty;
    public string Scopes { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}
