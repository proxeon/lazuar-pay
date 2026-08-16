using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Contracts.Events;
using Modules.Messaging.Contracts;

namespace Modules.Communications.Infrastructure.EventHandlers;

/// <summary>
/// Sends quotation/receipt email from denormalized <see cref="DocumentPublishedIntegrationEvent"/>
/// payload + local <c>communications.MessageTemplates</c>. No foreign-schema SQL.
/// </summary>
public class DocumentPublishedIntegrationEventHandler : IIntegrationEventHandler<DocumentPublishedIntegrationEvent>
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly IConfiguration _config;
    private readonly IEventBus _eventBus;

    public DocumentPublishedIntegrationEventHandler(
        CommunicationsDbContext dbContext,
        IConfiguration config,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _config = config;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(DocumentPublishedIntegrationEvent @event)
    {
        if (string.IsNullOrEmpty(@event.CustomerEmail) || string.IsNullOrEmpty(@event.TenantSlug))
            return;

        var preferredTemplate = @event.DocumentType switch
        {
            "Official Receipt" => "Official Receipt",
            "Draft Quotation" => "Quotation Ready",
            "Tax Invoice" => "Tax Invoice",
            "Credit Note" => "Credit Note",
            _ => null
        };
        if (preferredTemplate == null) return;

        var fallbackTemplate = preferredTemplate is "Tax Invoice" or "Credit Note"
            ? "Official Receipt"
            : null;

        var template = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.OrganizationId == @event.OrganizationId && t.Name == preferredTemplate)
            ?? (fallbackTemplate == null
                ? null
                : await _dbContext.MessageTemplates
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.OrganizationId == @event.OrganizationId && t.Name == fallbackTemplate));

        if (template == null) return;

        var exp = DocumentLinkSigner.ExpiryUnixSeconds(TimeSpan.FromDays(30));
        var secret = DocumentLinkSigner.ResolveSecret(_config["Jwt:Secret"]);
        var payload = DocumentLinkSigner.FinalDocumentPayload(@event.TenantSlug, @event.LedgerEntryId, exp);
        var sig = DocumentLinkSigner.Sign(secret, payload);
        var apiBaseUrl = _config["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";

        var documentLink = $"{apiBaseUrl}/public/billing/{@event.TenantSlug}/documents/{@event.LedgerEntryId}?sig={sig}&exp={exp}";

        var customerName = string.IsNullOrEmpty(@event.CustomerName) ? "Customer" : @event.CustomerName;
        var businessName = string.IsNullOrEmpty(@event.BusinessName) ? "Business" : @event.BusinessName;

        var htmlBody = (template.EmailBody ?? "")
            .Replace("{{customer_name}}", customerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{business_name}}", businessName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{document_link}}", documentLink, StringComparison.OrdinalIgnoreCase);

        var whatsappBody = (template.WhatsAppBody ?? "")
            .Replace("{{customer_name}}", customerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{business_name}}", businessName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{document_link}}", documentLink, StringComparison.OrdinalIgnoreCase);

        var subject = (template.Subject ?? "")
            .Replace("{{business_name}}", businessName, StringComparison.OrdinalIgnoreCase);

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            @event.OrganizationId,
            @event.CustomerEmail,
            null,
            subject,
            MarkdownParser.ToHtml(htmlBody),
            whatsappBody,
            template.Channel ?? "EMAIL"
        ));
        await _dbContext.SaveChangesAsync();
    }
}
