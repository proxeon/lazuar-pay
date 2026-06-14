using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Domain.Entities;

namespace Modules.Lhdn.Infrastructure.Workers;

/// <summary>
/// Parses LHDN official JSON files on system startup and seeds them into the database 
/// to power frontend dashboard dropdown menus.
/// </summary>
public class LhdnReferenceDataSeederJob : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _env;
    private readonly ILogger<LhdnReferenceDataSeederJob> _logger;

    public LhdnReferenceDataSeederJob(IServiceScopeFactory scopeFactory, IHostEnvironment env, ILogger<LhdnReferenceDataSeederJob> logger)
    {
        _scopeFactory = scopeFactory;
        _env = env;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LhdnDbContext>();

        var rootPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "../../../../lhdn_docs/codes"));
        if (!Directory.Exists(rootPath))
        {
            _logger.LogWarning("LHDN Reference Data directory not found at {Path}. Skipping seed.", rootPath);
            return;
        }

        if (!await db.TaxTypes.AnyAsync(cancellationToken))
        {
            var taxData = await File.ReadAllTextAsync(Path.Combine(rootPath, "tax_types.json"), cancellationToken);
            var taxTypes = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(taxData);
            
            if (taxTypes != null)
            {
                var entities = taxTypes.Select(t => new TaxType(t["Code"], t["Description"]));
                await db.TaxTypes.AddRangeAsync(entities, cancellationToken);
                _logger.LogInformation("Seeded LHDN Tax Types.");
            }
        }

        if (!await db.CountryCodes.AnyAsync(cancellationToken))
        {
            var countryData = await File.ReadAllTextAsync(Path.Combine(rootPath, "country_codes.json"), cancellationToken);
            var countries = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(countryData);
            
            if (countries != null)
            {
                var entities = countries.Select(c => new CountryCode(c["Code"], c["Country"]));
                await db.CountryCodes.AddRangeAsync(entities, cancellationToken);
                _logger.LogInformation("Seeded LHDN Country Codes.");
            }
        }

        if (!await db.MsicCodes.AnyAsync(cancellationToken))
        {
            var msicData = await File.ReadAllTextAsync(Path.Combine(rootPath, "classification_codes.json"), cancellationToken);
            var msicCodes = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(msicData);
            
            if (msicCodes != null)
            {
                var entities = msicCodes.Select(m => new MsicCode(m["Code"], m["Description"], "General"));
                await db.MsicCodes.AddRangeAsync(entities, cancellationToken);
                _logger.LogInformation("Seeded LHDN MSIC Classification Codes.");
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
