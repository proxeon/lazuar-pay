namespace Lazuar.Pay.One;

public sealed class WhoamiResponse
{
    public required string UserId { get; init; }
    public string? Email { get; init; }
    public bool IsPlatformAdmin { get; init; }
    public string? ActiveOrgId { get; init; }
    public IReadOnlyList<WhoamiTenant> Tenants { get; init; } = [];
}

public sealed class WhoamiTenant
{
    public required string Id { get; init; }
    public string? Slug { get; init; }
    public string? Name { get; init; }
    public string? Role { get; init; }
    public string? Status { get; init; }
}
