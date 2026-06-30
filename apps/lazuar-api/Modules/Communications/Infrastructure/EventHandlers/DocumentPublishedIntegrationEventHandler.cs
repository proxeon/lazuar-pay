using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Contracts.Events;
using Modules.Messaging.Contracts;

namespace Modules.Communications.Infrastructure.EventHandlers;

public class DocumentPublishedIntegrationEventHandler : IIntegrationEventHandler<DocumentPublishedIntegrationEvent>
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly ISqlConnectionFactory _sqlFactory;
    private readonly IConfiguration _config;
    private readonly IEventBus _eventBus;

    // Strictly typed record to satisfy .NET 10 nullability checks from Dapper
    private record DocumentData(string? TenantSlug, string? BusinessName, string? CustomerName, string? CustomerEmail);

    public DocumentPublishedIntegrationEventHandler(
        CommunicationsDbContext dbContext,
        [FromKeyedServices("CommunicationsSqlConnectionFactory")] ISqlConnectionFactory sqlFactory,
        IConfiguration config,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _sqlFactory = sqlFactory;
        _config = config;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(DocumentPublishedIntegrationEvent @event)
    {
        using var connection = _sqlFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();

        const string query = @"
            SELECT 
                org.""Slug"" as TenantSlug,
                org.""Name"" as BusinessName,
                t.""CustomerName"", 
                t.""CustomerEmail""
            FROM billing.""LedgerEntries"" e
            JOIN one.""Organizations"" org ON e.""OrganizationId"" = org.""Id""
            LEFT JOIN commerce.""TransactionLogs"" t ON e.""OrganizationId"" = t.""OrganizationId"" 
                AND (t.""ExternalReference"" = e.""ReferenceId"" OR t.""Id""::text = e.""ReferenceId"")
            WHERE e.""Id"" = @LedgerEntryId
            LIMIT 1";

        var data = await connection.QuerySingleOrDefaultAsync<DocumentData>(query, new { LedgerEntryId = @event.LedgerEntryId });
        
        if (data == null || string.IsNullOrEmpty(data.CustomerEmail) || string.IsNullOrEmpty(data.TenantSlug)) return;

        var templateName = @event.DocumentType == "Draft Quotation" ? "Quotation Ready" : "Official Receipt";

        var template = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.OrganizationId == @event.OrganizationId && t.Name == templateName);

        if (template == null) return;

        var exp = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var payload = $"{data.TenantSlug}:{@event.LedgerEntryId}:{exp}";
        var secret = _config["Jwt:Secret"] ?? "secure_development_key_minimum_32_characters_long";
        
        var sig = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var apiBaseUrl = _config["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
        
        var documentLink = $"{apiBaseUrl}/public/billing/{data.TenantSlug}/documents/{@event.LedgerEntryId}?sig={sig}&exp={exp}";

        var customerName = data.CustomerName ?? "Customer";
        var businessName = data.BusinessName ?? "Business";

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
            data.CustomerEmail,
            null,
            subject,
            MarkdownParser.ToHtml(htmlBody),
            whatsappBody,
            template.Channel ?? "EMAIL"
        ));
    }
}
