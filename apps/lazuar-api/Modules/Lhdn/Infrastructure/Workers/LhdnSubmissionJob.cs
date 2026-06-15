using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Workers;

public class LhdnSubmissionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LhdnSubmissionJob> _logger;
    private readonly IConfiguration _configuration;

    public LhdnSubmissionJob(IServiceScopeFactory scopeFactory, ILogger<LhdnSubmissionJob> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
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
        var vault = scope.ServiceProvider.GetRequiredService<ICertificateVaultService>();
        var xmlSigner = scope.ServiceProvider.GetRequiredService<IXmlSignatureService>();

        var pendingDocs = await db.TaxDocuments
            .Where(d => d.ValidationStatus == "PENDING")
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

                var xmlDoc = new XmlDocument
                {
                    PreserveWhitespace = true
                };
                xmlDoc.LoadXml(doc.RawXmlContent);

                if (!string.IsNullOrEmpty(config.EncryptedPfxBase64) && !string.IsNullOrEmpty(config.PfxPasswordCiphertext))
                {
                    using var cert = vault.GetDecryptedCertificate(config.EncryptedPfxBase64, config.PfxPasswordCiphertext);
                    xmlSigner.SignDocument(xmlDoc, cert);
                }

                var finalXmlBytes = Encoding.UTF8.GetBytes(xmlDoc.OuterXml);

                var documentHashBytes = SHA256.HashData(finalXmlBytes);
                var documentHashHex = Convert.ToHexString(documentHashBytes).ToLowerInvariant();
                var base64Document = Convert.ToBase64String(finalXmlBytes);

                var payload = new
                {
                    documents = new[]
                    {
                        new
                        {
                            format = "XML",
                            documentHash = documentHashHex,
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
                    doc.MarkAsFailed(result.ErrorMessage ?? "Unknown gateway error.");
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
