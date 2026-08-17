using System;
using Microsoft.Extensions.Configuration;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Infrastructure.Services;

public class LhdnLinkService : ILhdnLinkService
{
    public const string ProductionPortalHost = "https://myinvois.hasil.gov.my";
    public const string SandboxPortalHost = "https://preprod.myinvois.hasil.gov.my";

    private readonly IConfiguration _configuration;

    public LhdnLinkService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetPortalUrl(string? environment = null)
    {
        if (IsProduction(environment))
        {
            return _configuration["Lhdn:ProdPortalUrl"]?.TrimEnd('/') ?? ProductionPortalHost;
        }

        return _configuration["Lhdn:PortalUrl"]?.TrimEnd('/') ?? SandboxPortalHost;
    }

    internal static bool IsProduction(string? environment) =>
        string.Equals(environment, "PROD", StringComparison.OrdinalIgnoreCase)
        || string.Equals(environment, "PRODUCTION", StringComparison.OrdinalIgnoreCase);
}
