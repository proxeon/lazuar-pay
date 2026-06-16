using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Ports;

namespace Modules.Lhdn.Infrastructure.Workers;

public class LhdnSubmissionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LhdnSubmissionJob> _logger;

    public LhdnSubmissionJob(IServiceScopeFactory scopeFactory, ILogger<LhdnSubmissionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingDocumentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in LhdnSubmissionJob.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessPendingDocumentsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LhdnDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<ILhdnGatewayAdapter>();

        var now = DateTime.UtcNow;
        var pendingDocs = await db.TaxDocuments
            .Where(d => d.ValidationStatus == "PENDING" && (d.NextPollAt == null || d.NextPollAt <= now))
            .OrderBy(d => d.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (!pendingDocs.Any()) return;

        foreach (var doc in pendingDocs)
        {
            try
            {
                var config = await db.TenantConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.OrganizationId == doc.OrganizationId, ct);
                if (config == null || string.IsNullOrWhiteSpace(config.MyInvoisClientId) || string.IsNullOrWhiteSpace(config.MyInvoisClientSecret))
                {
                    doc.MarkAsFailed("Tenant configuration or API credentials missing.");
                    continue;
                }

                var base64Document = Convert.ToBase64String(Encoding.UTF8.GetBytes(doc.RawXmlContent));

                var payload = new
                {
                    documents = new[]
                    {
                        new
                        {
                            format = "XML", 
                            documentHash = doc.DocumentHash, 
                            codeNumber = doc.InternalReferenceId,
                            document = base64Document
                        }
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                
                var token = await gateway.GetTokenAsync(config.OrganizationId, config.MyInvoisClientId, config.MyInvoisClientSecret, config.IntermediaryMode, config.SupplierTin, ct);
                var result = await gateway.SubmitDocumentAsync(config.MyInvoisClientId, token, jsonPayload, config.IntermediaryMode, config.SupplierTin, ct);

                if (result.Success && !string.IsNullOrEmpty(result.SubmissionUid))
                {
                    doc.MarkAsSubmitted(result.SubmissionUid, result.Uuid);
                }
                else
                {
                    if (result.RetryAfterSeconds.HasValue)
                    {
                        doc.DelayPendingSubmission(result.RetryAfterSeconds.Value);
                    }
                    else
                    {
                        doc.MarkAsFailed(result.ErrorMessage ?? "Unknown gateway error.");
                    }
                }
            }
            catch (Exception ex)
            {
                doc.MarkAsFailed(ex.Message);
            }
            finally
            {
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
