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
        return _configuration["App:CommunityUrl"]?.TrimEnd('/') ?? "http://localhost:3021";
    }
}
