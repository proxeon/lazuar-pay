using System;
using Microsoft.Extensions.Configuration;

namespace Modules.Lhdn.Infrastructure.Services;

internal static class LhdnEnvironmentUrls
{
    public const string ProductionApi = "https://api.myinvois.hasil.gov.my";
    public const string SandboxApi = "https://preprod-api.myinvois.hasil.gov.my";
    public const string ProductionPortal = "https://myinvois.hasil.gov.my";
    public const string SandboxPortal = "https://preprod.myinvois.hasil.gov.my";

    public static bool IsProduction(string? environment) =>
        string.Equals(environment, "PROD", StringComparison.OrdinalIgnoreCase)
        || string.Equals(environment, "PRODUCTION", StringComparison.OrdinalIgnoreCase);

    public static string ApiBaseUrl(IConfiguration configuration, string? environment)
    {
        if (IsProduction(environment))
            return FirstUrl(configuration["Lhdn:ProdBaseUrl"], ProductionApi);

        return FirstUrl(configuration["Lhdn:BaseUrl"], SandboxApi);
    }

    public static string PortalUrl(IConfiguration configuration, string? environment)
    {
        if (IsProduction(environment))
            return FirstUrl(configuration["Lhdn:ProdPortalUrl"], ProductionPortal);

        return FirstUrl(configuration["Lhdn:PortalUrl"], SandboxPortal);
    }

    private static string FirstUrl(string? configured, string fallback)
    {
        var trimmed = configured?.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }
}
