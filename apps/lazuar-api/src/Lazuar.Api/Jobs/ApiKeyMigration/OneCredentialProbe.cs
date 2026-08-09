namespace Lazuar.Api.Jobs.ApiKeyMigration;

/// <summary>
/// Minimal projection of an existing <c>one.ApiCredentials</c> row for collision checks.
/// </summary>
public sealed class OneCredentialProbe
{
    public Guid Id { get; init; }
    public string KeyHash { get; init; } = string.Empty;
    public Guid OrganizationId { get; init; }
}
