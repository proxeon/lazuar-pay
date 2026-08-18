using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Buyer-facing portal / checkout host. Default matches <c>App:ClientUrl</c> in appsettings (port 3004).
/// </summary>
public static class AppClientUrl
{
    public const string DevelopmentFallback = "http://localhost:3004";

    public static string Resolve(IConfiguration? configuration)
    {
        var value = configuration?["App:ClientUrl"];
        if (string.IsNullOrWhiteSpace(value))
        {
            return DevelopmentFallback;
        }

        return value.TrimEnd('/');
    }
}
