namespace Lazuar.Pay.Identity.Client;

internal sealed class OneCallResult<T>
{
    public T? Value { get; init; }
    public int? StatusCode { get; init; }
    public string? Detail { get; init; }
    public bool TimedOut { get; init; }
    public bool TransportFailed { get; init; }
}
