namespace Lazuar.Pay.One;

internal sealed class OneAuthzCheckRequest
{
    public required string Relation { get; init; }
    public required OneAuthzObject Object { get; init; }
}

internal sealed class OneAuthzObject
{
    public required string Type { get; init; }
    public required string Id { get; init; }
}

internal sealed class OneAuthzCheckResponse
{
    public bool Allowed { get; set; }
}

public sealed class OrgReadyResponse
{
    public required string OrgId { get; init; }
    public bool Ready { get; init; }
}
