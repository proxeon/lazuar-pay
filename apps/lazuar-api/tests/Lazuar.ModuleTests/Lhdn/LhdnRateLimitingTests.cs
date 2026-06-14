using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure;
using Modules.Lhdn.Infrastructure.Workers;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

/// <summary>
/// Verifies that the LhdnSubmissionJob strictly processes a maximum of 90 requests per minute
/// to prevent triggering LHDN's 100 RPM rate limit block.
/// </summary>
[TestFixture]
public class LhdnRateLimitingTests
{
    [Test]
    public async Task SubmissionJob_ShouldEnforceStrictRateLimit_WhenProcessing150Invoices()
    {
        var services = new ServiceCollection();
        
        services.AddDbContext<LhdnDbContext>(options => 
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var gatewayMock = Substitute.For<ILhdnGatewayAdapter>();
        gatewayMock.GetTokenAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("mock_token");
            
        // Fix 1: Passed the missing 'clientId', 'isIntermediary', and 'tenantTin' parameter signatures
        gatewayMock.SubmitDocumentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new LhdnSubmissionResult(true, "sub_123", "uuid_123", null));

        var vaultMock = Substitute.For<ICertificateVaultService>();
        var signerMock = Substitute.For<IXmlSignatureService>();

        services.AddSingleton(gatewayMock);
        services.AddSingleton(vaultMock);
        services.AddSingleton(signerMock);

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LhdnDbContext>();
            var orgId = Guid.CreateVersion7();
            
            // Fix 2: Used the updated TenantConfig constructor fields 
            // (organizationId, intermediaryMode, supplierTin, idType, idValue, environment, msicCode)
            var config = new LhdnTenantConfig(orgId, false, "C1234567890", "BRN", "202401234567", "SANDBOX", "62010");
            config.UpdateApiCredentials("mock_client_id", "mock_client_secret");
            db.TenantConfigs.Add(config);
            
            for (int i = 0; i < 150; i++)
            {
                db.TaxDocuments.Add(new TaxDocument(orgId, $"INV-{i}", "hash", "<invoice></invoice>"));
            }
            await db.SaveChangesAsync();
        }

        var configMock = Substitute.For<IConfiguration>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new LhdnSubmissionJob(scopeFactory, NullLogger<LhdnSubmissionJob>.Instance, configMock);

        var stopwatch = Stopwatch.StartNew();
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(110)); 
        
        try
        {
            await job.StartAsync(cts.Token);
            while (!cts.IsCancellationRequested)
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LhdnDbContext>();
                var pendingCount = await db.TaxDocuments.CountAsync(d => d.ValidationStatus == "PENDING", CancellationToken.None);
                
                if (pendingCount == 0)
                {
                    cts.Cancel();
                }
                await Task.Delay(1000, CancellationToken.None);
            }
        }
        catch (TaskCanceledException) { }

        stopwatch.Stop();

        // 150 requests at 666ms per request should take approximately 100 seconds.
        // If it completes in less than 95 seconds, the rate limit is broken.
        stopwatch.Elapsed.TotalSeconds.Should().BeGreaterThan(95);

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LhdnDbContext>();
            var pendingCount = await db.TaxDocuments.CountAsync(d => d.ValidationStatus == "PENDING");
            pendingCount.Should().Be(0);
        }
    }
}
