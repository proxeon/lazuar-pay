namespace Lazuar.Pay.Identity.Client;

public sealed class OneOptions
{
    public const string Section = "One";

    /// <summary>One API prefix, e.g. http://localhost:8080/api/v1. Client appends /me.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080/api/v1";

    public int TimeoutSeconds { get; set; } = 5;
}
