namespace Lazuar.Pay.Identity.Client;

public sealed class OneOptions
{
    public const string Section = "One";

    /// <summary>One API prefix, e.g. http://localhost:8080/api/v1. Client appends /me.</summary>
    public string BaseUrl { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Process-bound One machine key for hosted jobs. Must be <c>lzr_sk_</c>.
    /// Never applied to the interactive <see cref="OneClient"/>.
    /// Bound to <see cref="WorkerOrgId"/> — one tenant, not a god-key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>One tenant this process may speak for when <see cref="ApiKey"/> is set.</summary>
    public string? WorkerOrgId { get; set; }
}
