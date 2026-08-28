using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Hosting;

internal static class PayCors
{
    public const string Key = "Pay:CorsOrigins";

    public static readonly string[] DevelopmentOrigins =
    [
        "http://localhost:5178",
        "http://127.0.0.1:5178",
        "http://localhost:5179",
        "http://127.0.0.1:5179",
        "http://localhost:4178",
        "http://127.0.0.1:4178",
        "http://localhost:4179",
        "http://127.0.0.1:4179"
    ];

    public static void Add(WebApplicationBuilder builder)
    {
        var origins = Resolve(builder.Configuration[Key], builder.Environment.EnvironmentName);
        builder.Services.AddCors(o =>
        {
            o.AddDefaultPolicy(p =>
                p.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });
    }

    public static string[] Resolve(string? raw, string environmentName)
    {
        if (TryParse(raw, out var origins))
        {
            return origins;
        }

        if (string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            return DevelopmentOrigins;
        }

        throw new InvalidOperationException("Pay:CorsOrigins must be configured in Production and Staging.");
    }

    public static bool TryParse(string? raw, out string[] origins)
    {
        origins = [];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        origins = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return origins.Length > 0;
    }
}
