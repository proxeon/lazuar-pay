namespace Lazuar.Api.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// The primary client-facing frontend URL (portal / public checkout surfaces, typically port 3020).
    /// </summary>
    public string ClientUrl { get; init; } = "http://localhost:3020";

    /// <summary>
    /// Base URL of the API (used for constructing webhook callback URLs and magic-link redirect URIs).
    /// </summary>
    public string ApiBaseUrl { get; init; } = "http://localhost:8080/api/v1";

    /// <summary>
    /// List of allowed CORS origins.
    /// </summary>
    public string CorsOrigins { get; init; } = "";
}
