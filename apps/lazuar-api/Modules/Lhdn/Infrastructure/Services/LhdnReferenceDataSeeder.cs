using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Modules.Lhdn.Infrastructure.Services;

public sealed class LhdnReferenceDataSeeder
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LhdnReferenceDataSeeder> _logger;

    public LhdnReferenceDataSeeder(
        IHostEnvironment hostEnvironment,
        IConfiguration configuration,
        ILogger<LhdnReferenceDataSeeder> logger)
    {
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
        _logger = logger;
    }

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var configuredPath = _configuration["Lhdn:ReferenceDataPath"] ?? "lhdn_docs/codes";
        
        // Resolve path relative to application host content root if not rooted
        var referenceDataPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configuredPath));

        if (!Directory.Exists(referenceDataPath))
        {
            _logger.LogWarning("LHDN Reference Data directory not found at {Path}. Skipping seed.", referenceDataPath);
            return Task.CompletedTask;
        }

        _logger.LogInformation("LHDN Reference Data directory found at {Path}. Initializing seed operations.", referenceDataPath);
        return Task.CompletedTask;
    }
}
