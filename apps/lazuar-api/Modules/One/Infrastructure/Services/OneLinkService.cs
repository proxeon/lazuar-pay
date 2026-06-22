using Microsoft.Extensions.Configuration;
using Modules.One.Application;

namespace Modules.One.Infrastructure.Services;

public class OneLinkService : IOneLinkService
{
    private readonly IConfiguration _configuration;

    public OneLinkService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetAuthUrl()
    {
        return _configuration["App:AuthUrl"]?.TrimEnd('/') ?? "http://localhost:3001";
    }

    public string GetOpsUrl()
    {
        return _configuration["App:OpsUrl"]?.TrimEnd('/') ?? "http://localhost:3003";
    }
}
