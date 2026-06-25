// apps/lazuar-api/Modules/Community/Infrastructure/Services/CommunityLinkService.cs
using Microsoft.Extensions.Configuration;
using Modules.Community.Application;

namespace Modules.Community.Infrastructure.Services;

public class CommunityLinkService : ICommunityLinkService
{
    private readonly IConfiguration _configuration;

    public CommunityLinkService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetCommunityBaseUrl()
    {
        // Read directly from the configuration, falling back to the new portal port if missing
        return _configuration["App:ClientUrl"]?.TrimEnd('/') ?? "http://localhost:3004";
    }
}
