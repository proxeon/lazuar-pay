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

    public string GetPortalUrl(string? environment = null) =>
        LhdnEnvironmentUrls.PortalUrl(_configuration, environment);
}
