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

    public string GetClientBaseUrl()
    {
        return _configuration["App:ClientUrl"]?.TrimEnd('/') ?? "http://localhost:3004";
    }
}
