using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Dapper;
using Lazuar.ApiTypes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Contracts;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Infrastructure.Services;

public class PortalDocumentQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IBillingQueryService _billingQueryService;
    private readonly IConfiguration _configuration;

    public PortalDocumentQueryService(
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        ICrmQueryService crmQueryService,
        IBillingQueryService billingQueryService,
        IConfiguration configuration)
    {
        _connectionFactory = connectionFactory;
        _crmQueryService = crmQueryService;
        _billingQueryService = billingQueryService;
        _configuration = configuration;
    }

    public async Task<PortalDocumentsResponse> ListForBuyerAsync(
        Guid organizationId,
        Guid referenceSubscriptionId,
        string tenantSlug)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string profileSql = @"
            SELECT ""ClientProfileId"" FROM commerce.""Subscriptions""
            WHERE ""Id"" = @SubId AND ""OrganizationId"" = @OrgId LIMIT 1";

        var clientProfileId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            profileSql, new { SubId = referenceSubscriptionId, OrgId = organizationId });

        if (clientProfileId == null)
            return new PortalDocumentsResponse { Items = new List<PortalDocumentDto>() };

        var profile = await _crmQueryService.GetClientProfileAsync(clientProfileId.Value);
        var email = profile?.Email ?? "";

        var profileIds = new HashSet<Guid> { clientProfileId.Value };
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _crmQueryService.GetClientProfileByEmailAsync(organizationId, email);
            if (byEmail != null && Guid.TryParse(byEmail.Id, out var emailProfileId))
                profileIds.Add(emailProfileId);
        }

        var profileIdArray = profileIds.ToArray();

        const string refsSql = @"
            SELECT ""ExternalReference"", ""Id""::text AS LogId, ""SubscriptionId""
            FROM commerce.""TransactionLogs""
            WHERE ""OrganizationId"" = @OrgId
              AND (
                    ""CustomerEmail"" = @Email
                 OR ""SubscriptionId"" IN (
                        SELECT ""Id"" FROM commerce.""Subscriptions""
                        WHERE ""OrganizationId"" = @OrgId AND ""ClientProfileId"" = ANY(@ProfileIds)
                    )
              )";

        var refs = (await connection.QueryAsync<RawTxRef>(refsSql, new
        {
            OrgId = organizationId,
            Email = email,
            ProfileIds = profileIdArray
        })).ToList();

        var referenceIds = refs
            .SelectMany(r => new[] { r.ExternalReference, r.LogId })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var ledgers = await _billingQueryService.GetDocumentsByReferenceIdsAsync(organizationId, referenceIds);

        var refToSub = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var tx in refs)
        {
            if (tx.SubscriptionId is not { } subId || subId == Guid.Empty)
                continue;
            if (!string.IsNullOrWhiteSpace(tx.ExternalReference))
                refToSub[tx.ExternalReference] = subId;
            if (!string.IsNullOrWhiteSpace(tx.LogId))
                refToSub[tx.LogId] = subId;
        }

        var items = new List<PortalDocumentDto>();
        var latestBySub = new Dictionary<string, PortalDocumentDto>(StringComparer.Ordinal);
        foreach (var ledger in ledgers)
        {
            var dto = new PortalDocumentDto
            {
                Id = ledger.Id.ToString(),
                Document_number = ledger.CustomerDocumentNumber,
                Type = Classify(ledger),
                Issued_at = new DateTimeOffset(DateTime.SpecifyKind(ledger.Timestamp, DateTimeKind.Utc)),
                Amount = (double)ledger.Amount,
                Currency = ledger.Currency,
                Lhdn_status = ledger.LhdnValidationStatus,
                Download_url = BuildFinalUrl(tenantSlug, ledger.Id)
            };
            items.Add(dto);

            if (dto.Type is "Official Receipt" or "Tax Invoice" or "Invoice"
                && refToSub.TryGetValue(ledger.ReferenceId, out var subId))
            {
                var key = subId.ToString();
                if (!latestBySub.TryGetValue(key, out var existing) || dto.Issued_at > existing.Issued_at)
                    latestBySub[key] = dto;
            }
        }

        LastLatestBySubscription = latestBySub;

        const string quotesSql = @"
            SELECT c.""Id"", c.""DocumentNumber"", c.""CreatedAt"", c.""AdHocLineItems""
            FROM commerce.""CheckoutSessions"" c
            WHERE c.""OrganizationId"" = @OrgId
              AND c.""ProductId"" IS NULL
              AND c.""ClientProfileId"" = ANY(@ProfileIds)
              AND c.""DocumentNumber"" IS NOT NULL
            ORDER BY c.""CreatedAt"" DESC";

        var quotes = (await connection.QueryAsync<RawQuote>(quotesSql, new
        {
            OrgId = organizationId,
            ProfileIds = profileIdArray
        })).ToList();

        foreach (var quote in quotes)
        {
            items.Add(new PortalDocumentDto
            {
                Id = quote.Id.ToString(),
                Document_number = quote.DocumentNumber,
                Type = "Proforma",
                Issued_at = new DateTimeOffset(DateTime.SpecifyKind(quote.CreatedAt, DateTimeKind.Utc)),
                Amount = SumQuote(quote.AdHocLineItems),
                Currency = "MYR",
                Download_url = BuildDraftUrl(tenantSlug, quote.Id)
            });
        }

        return new PortalDocumentsResponse
        {
            Items = items
                .OrderByDescending(i => i.Issued_at)
                .ToList()
        };
    }

    public IReadOnlyDictionary<string, PortalDocumentDto> LastLatestBySubscription { get; private set; } =
        new Dictionary<string, PortalDocumentDto>();

    public void AttachLatestToSubscriptions(AggregatedPortalDataResponse portal)
    {
        foreach (var sub in portal.Subscriptions)
        {
            if (!LastLatestBySubscription.TryGetValue(sub.Id, out var latest)
                || string.IsNullOrWhiteSpace(latest.Download_url))
            {
                continue;
            }

            sub.Document_url = latest.Download_url;
            sub.Document_label = latest.Type switch
            {
                "Tax Invoice" => "Download tax invoice",
                "Invoice" => "Download invoice",
                _ => "Download receipt"
            };
        }
    }

    private static string Classify(LedgerDocumentIdentity ledger)
    {
        if (ledger.ReferenceType is "GATEWAY_REFUND" or "LHDN_CANCELLATION"
            || DocumentSeries.IsCreditNoteNumber(ledger.CustomerDocumentNumber))
        {
            return "Credit Note";
        }

        if (ledger.CustomerType == "B2B" || DocumentSeries.IsInvoiceNumber(ledger.CustomerDocumentNumber))
        {
            return string.Equals(ledger.LhdnValidationStatus, "VALID", StringComparison.OrdinalIgnoreCase)
                ? "Tax Invoice"
                : "Invoice";
        }

        return "Official Receipt";
    }

    private string BuildFinalUrl(string tenantSlug, Guid ledgerEntryId)
    {
        var secret = DocumentLinkSigner.ResolveSecret(_configuration["Jwt:Secret"]);
        var exp = DocumentLinkSigner.ExpiryUnixSeconds(TimeSpan.FromDays(30));
        var payload = DocumentLinkSigner.FinalDocumentPayload(tenantSlug, ledgerEntryId, exp);
        var sig = DocumentLinkSigner.Sign(secret, payload);
        var apiBaseUrl = _configuration["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
        return $"{apiBaseUrl}/public/billing/{tenantSlug}/documents/{ledgerEntryId}?sig={sig}&exp={exp}";
    }

    private string BuildDraftUrl(string tenantSlug, Guid sessionId)
    {
        var secret = DocumentLinkSigner.ResolveSecret(_configuration["Jwt:Secret"]);
        var exp = DocumentLinkSigner.ExpiryUnixSeconds(TimeSpan.FromDays(7));
        var payload = DocumentLinkSigner.DraftDocumentPayload(tenantSlug, sessionId, exp);
        var sig = DocumentLinkSigner.Sign(secret, payload);
        var apiBaseUrl = _configuration["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
        return $"{apiBaseUrl}/public/billing/{tenantSlug}/documents/draft/{sessionId}?sig={sig}&exp={exp}";
    }

    private static double SumQuote(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<List<CustomLineItemDto>>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower });
            return items?.Sum(i => i.Unit_price * i.Quantity) ?? 0;
        }
        catch (System.Text.Json.JsonException)
        {
            return 0;
        }
    }

    private record RawTxRef(string? ExternalReference, string? LogId, Guid? SubscriptionId);
    private record RawQuote(Guid Id, string? DocumentNumber, DateTime CreatedAt, string? AdHocLineItems);
}
