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

        // FIX: Point local development to the Next.js Storefront (3021), not the Admin app (3020)
        return apiBaseUrl.Contains("lazuar.com")
            ? "https://community.lazuar.com"
            : "http://localhost:3021";
    }
}
