using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Communications.Contracts;
using Lazuar.ApiTypes;

namespace Modules.Communications.Infrastructure.Services;

public class CommunicationsQueryService : ICommunicationsQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ISecretVault _secretVault;

    private record RawMessageTemplate(Guid Id, string Name, string Channel, string Subject, string EmailBody, string WhatsAppBody, bool IsDefault, string? RequiredVariables, string? OptionalVariables, DateTime UpdatedAt);
    private record RawEmailConfig(string ApiKey, string SenderEmail, bool IsActive);

    public CommunicationsQueryService(
        [FromKeyedServices("CommunicationsSqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        ISecretVault secretVault)
    {
        _connectionFactory = connectionFactory;
        _secretVault = secretVault;
    }

    public async Task<IEnumerable<MessageTemplateDto>> GetAllTemplatesAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"Name\", \"Channel\", \"Subject\", \"EmailBody\", \"WhatsAppBody\", \"IsDefault\", \"RequiredVariables\"::text, \"OptionalVariables\"::text, \"UpdatedAt\" FROM communications.\"MessageTemplates\" WHERE \"OrganizationId\" = @OrgId ORDER BY \"Name\"";
        var rawTemplates = await connection.QueryAsync<RawMessageTemplate>(sql, new { OrgId = organizationId });
        return rawTemplates.Select(MapToDto);
    }

    public async Task<MessageTemplateDto?> GetTemplateByNameAsync(Guid organizationId, string name)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"Name\", \"Channel\", \"Subject\", \"EmailBody\", \"WhatsAppBody\", \"IsDefault\", \"RequiredVariables\"::text, \"OptionalVariables\"::text, \"UpdatedAt\" FROM communications.\"MessageTemplates\" WHERE \"OrganizationId\" = @OrgId AND \"Name\" = @Name LIMIT 1";
        var rawTemplate = await connection.QuerySingleOrDefaultAsync<RawMessageTemplate>(sql, new { OrgId = organizationId, Name = name });
        return rawTemplate != null ? MapToDto(rawTemplate) : null;
    }

    private static MessageTemplateDto MapToDto(RawMessageTemplate raw)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };
        var reqVars = string.IsNullOrWhiteSpace(raw.RequiredVariables) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(raw.RequiredVariables, options) ?? new List<string>();
        var optVars = string.IsNullOrWhiteSpace(raw.OptionalVariables) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(raw.OptionalVariables, options) ?? new List<string>();

        return new MessageTemplateDto
        {
            Id = raw.Id.ToString(),
            Name = raw.Name,
            Channel = raw.Channel,
            Subject = raw.Subject,
            Email_body = raw.EmailBody,
            Whatsapp_body = raw.WhatsAppBody,
            Is_default = raw.IsDefault,
            Required_variables = reqVars,
            Optional_variables = optVars,
            Updated_at = new DateTimeOffset(raw.UpdatedAt)
        };
    }

    public Task<IEnumerable<TemplateVariableCategoryDto>> GetTemplateVariablesAsync()
    {
        var categories = new List<TemplateVariableCategoryDto>
        {
            new TemplateVariableCategoryDto
            {
                Title = "Customer Profile Context",
                Items = new List<TemplateVariableDto>
                {
                    new TemplateVariableDto { Tag = "{{customer_name}}", Description = "The full display name of the member." },
                    new TemplateVariableDto { Tag = "{{customer_email}}", Description = "The registered email address of the member." },
                    new TemplateVariableDto { Tag = "{{customer_phone}}", Description = "The phone number of the member." }
                }
            },
            new TemplateVariableCategoryDto
            {
                Title = "Billing & Subscriptions",
                Items = new List<TemplateVariableDto>
                {
                    new TemplateVariableDto { Tag = "{{business_name}}", Description = "The workspace / merchant display name." },
                    new TemplateVariableDto { Tag = "{{plan_name}}", Description = "The subscription name (e.g. Premium Tier)." },
                    new TemplateVariableDto { Tag = "{{amount}}", Description = "Product list price, formatted 0.00." },
                    new TemplateVariableDto { Tag = "{{total_price}}", Description = "Same as amount until invoice totals exist." },
                    new TemplateVariableDto { Tag = "{{currency}}", Description = "ISO currency code (e.g. MYR)." },
                    new TemplateVariableDto { Tag = "{{days_overdue}}", Description = "Calendar days past NextBillingDate. Pre-dunning is 0." },
                    new TemplateVariableDto { Tag = "{{current_period_end}}", Description = "Next billing / paid-through date (NextBillingDate)." },
                    new TemplateVariableDto { Tag = "{{update_payment_link}}", Description = "Hosted update-payment URL for this subscription." },
                    new TemplateVariableDto { Tag = "{{renewal_link}}", Description = "Same as update-payment link (recovery checkout)." },
                    new TemplateVariableDto { Tag = "{{portal_magic_link}}", Description = "Secure, 24-hour auto-login link to the subscriber portal (dunning and lifecycle)." }
                }
            },
            new TemplateVariableCategoryDto
            {
                Title = "Fulfillment Assets",
                Items = new List<TemplateVariableDto>
                {
                    new TemplateVariableDto { Tag = "{{fulfillment_url}}", Description = "Cloudflare R2 Download Link." }
                }
            }
        };

        return Task.FromResult<IEnumerable<TemplateVariableCategoryDto>>(categories);
    }

    public async Task<bool> HasValidEmailConfigAsync(Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 1 
            FROM communications.""TenantEmailConfigurations"" 
            WHERE ""OrganizationId"" = @TenantId 
              AND ""IsActive"" = true 
              AND ""ApiKey"" IS NOT NULL AND ""ApiKey"" != ''
              AND ""SenderEmail"" IS NOT NULL AND ""SenderEmail"" != ''
            LIMIT 1";

        var result = await connection.QuerySingleOrDefaultAsync<int?>(sql, new { TenantId = tenantId });
        return result.HasValue;
    }

    public async Task<EmailConfigDto?> GetEmailConfigAsync(Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""ApiKey"", ""SenderEmail"", ""IsActive""
            FROM communications.""TenantEmailConfigurations"" 
            WHERE ""OrganizationId"" = @TenantId 
            LIMIT 1";

        var result = await connection.QuerySingleOrDefaultAsync<RawEmailConfig>(sql, new { TenantId = tenantId });

        if (result == null) return null;

        var hasKey = !string.IsNullOrWhiteSpace(result.ApiKey);
        string? hint = null;
        if (hasKey)
        {
            // Prefer decrypting to show last-4 of the real key; fall back to ciphertext suffix.
            try
            {
                var plain = _secretVault.Decrypt(result.ApiKey);
                hint = plain.Length <= 4 ? "****" : $"…{plain[^4..]}";
            }
            catch
            {
                hint = result.ApiKey.Length <= 4 ? "****" : $"…{result.ApiKey[^4..]}";
            }
        }

        return new EmailConfigDto
        {
            Has_api_key = hasKey,
            Api_key_hint = hint,
            Sender_email = result.SenderEmail,
            Is_active = result.IsActive
        };
    }

    public async Task<TenantEmailCredentials?> GetEmailConfigCredentialsAsync(Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""ApiKey"", ""SenderEmail"", ""IsActive""
            FROM communications.""TenantEmailConfigurations"" 
            WHERE ""OrganizationId"" = @TenantId 
            LIMIT 1";

        var result = await connection.QuerySingleOrDefaultAsync<RawEmailConfig>(sql, new { TenantId = tenantId });
        if (result == null || string.IsNullOrWhiteSpace(result.ApiKey)) return null;

        string plainKey;
        try
        {
            plainKey = _secretVault.Decrypt(result.ApiKey);
        }
        catch
        {
            // Backward-compat: treat unencrypted legacy rows as plaintext until re-saved.
            plainKey = result.ApiKey;
        }

        return new TenantEmailCredentials(plainKey, result.SenderEmail, result.IsActive);
    }
}
