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
        var apiBaseUrl = _configuration["App:ApiBaseUrl"] ?? "";
        return apiBaseUrl.Contains("lazuar.com") 
            ? "https://community.lazuar.com" 
            : "http://localhost:3020";
    }
}
