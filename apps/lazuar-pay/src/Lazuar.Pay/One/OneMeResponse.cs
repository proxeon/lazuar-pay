namespace Lazuar.Pay.One;

internal sealed class OneMeResponse
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public string? ActiveTenantId { get; set; }
    public string? ActiveRole { get; set; }
    public List<OneMeTenant> Tenants { get; set; } = [];
}

internal sealed class OneMeTenant
{
    public string? Id { get; set; }
    public string? Slug { get; set; }
    public string? Name { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
}
