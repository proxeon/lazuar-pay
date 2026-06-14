using Microsoft.Extensions.Configuration;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Infrastructure.Services;

public class LhdnLinkService : ILhdnLinkService
{
    private readonly IConfiguration _configuration;

    public LhdnLinkService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetPortalUrl()
    {
        return _configuration["Lhdn:PortalUrl"]?.TrimEnd('/') ?? "https://preprod.myinvois.hasil.gov.my";
    }
}
