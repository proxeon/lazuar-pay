namespace Lazuar.Api.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    public string AuthUrl { get; init; } = "http://localhost:3001";
    public string OpsUrl { get; init; } = "http://localhost:3003";
    public string CommunityUrl { get; init; } = "http://localhost:3021";
    public string ApiBaseUrl { get; init; } = "http://localhost:8080/api/v1";
    public string CorsOrigins { get; init; } = "";
}
