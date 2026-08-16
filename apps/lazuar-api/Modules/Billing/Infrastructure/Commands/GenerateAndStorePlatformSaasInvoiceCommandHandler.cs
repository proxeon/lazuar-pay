using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Infrastructure.Documents;
using Modules.Billing.Infrastructure.Services;
using Modules.One.Contracts;
using QuestPDF.Fluent;

namespace Modules.Billing.Infrastructure.Commands;

public class GenerateAndStorePlatformSaasInvoiceCommandHandler
    : ICommandHandler<GenerateAndStorePlatformSaasInvoiceCommand>
{
    private readonly BillingDbContext _dbContext;
    private readonly IR2StorageService _r2Service;
    private readonly IOneQueryService _oneQueryService;
    private readonly SaasOptions _saas;
    private readonly string _bucketName;

    public GenerateAndStorePlatformSaasInvoiceCommandHandler(
        BillingDbContext dbContext,
        IR2StorageService r2Service,
        IOneQueryService oneQueryService,
        [FromKeyedServices("BillingEventBus")] IEventBus eventBus,
        IOptions<SaasOptions> saas,
        IConfiguration config)
    {
        _dbContext = dbContext;
        _r2Service = r2Service;
        _oneQueryService = oneQueryService;
        _ = eventBus;
        _saas = saas.Value;
        _bucketName = config["R2_BUCKET_NAME"] ?? "lazuar-vault-test";
    }

    public async Task Handle(GenerateAndStorePlatformSaasInvoiceCommand request, CancellationToken ct)
    {
        var entry = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(
                e => e.Id == request.LedgerEntryId && e.OrganizationId == request.PayingOrganizationId,
                ct);

        if (entry == null)
            throw new InvalidOperationException("Ledger entry not found.");

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(request.PayingOrganizationId);
        var members = (await _oneQueryService.GetWorkspaceMembersAsync(request.PayingOrganizationId)).ToList();
        var admin = members.FirstOrDefault(m =>
            string.Equals(m.Role, "ADMIN", StringComparison.OrdinalIgnoreCase));
        var buyerName = workspace?.Name ?? "Workspace";
        var buyerEmail = admin?.Email ?? members.FirstOrDefault()?.Email ?? "";

        var invoiceNumber = entry.CustomerDocumentNumber
            ?? entry.TaxInvoiceId
            ?? entry.Id.ToString()[..8].ToUpperInvariant();

        var model = PlatformSaasInvoiceFactory.Create(
            _saas,
            invoiceNumber,
            entry.Timestamp,
            buyerName,
            buyerEmail,
            Math.Abs(entry.Lines.Where(l => l.AccountType == Domain.AccountTypes.ExpenseSoftwareSubscription)
                .Sum(l => l.Amount)),
            entry.Lines.FirstOrDefault()?.Currency ?? _saas.Plan.Currency);

        var pdfBytes = new BaseInvoiceDocument(model).GeneratePdf();
        var storageKey = $"vault/{request.PayingOrganizationId}/documents/{request.LedgerEntryId}.pdf";
        using var stream = new MemoryStream(pdfBytes);
        await _r2Service.UploadAsync(stream, _bucketName, storageKey, "application/pdf", ct);
    }
}
