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
                    new TemplateVariableDto { Tag = "{{amount}}", Description = "Gross this cycle (seats × snapshot + SST when the merchant is SST-registered). Same number dunning uses." },
                    new TemplateVariableDto { Tag = "{{total_price}}", Description = "Same as amount — Gross this cycle (seats × snapshot + SST when the merchant is SST-registered)." },
                    new TemplateVariableDto { Tag = "{{currency}}", Description = "ISO currency code (e.g. MYR)." },
                    new TemplateVariableDto { Tag = "{{days_overdue}}", Description = "Calendar days past NextBillingDate. Pre-dunning is 0." },
                    new TemplateVariableDto { Tag = "{{current_period_end}}", Description = "Next billing / paid-through date (NextBillingDate)." },
                    new TemplateVariableDto { Tag = "{{update_payment_link}}", Description = "Hosted update-payment URL for this subscription." },
                    new TemplateVariableDto { Tag = "{{renewal_link}}", Description = "Hosted pay-this-cycle checkout when minted; otherwise the update-payment page." },
                    new TemplateVariableDto { Tag = "{{checkout_url}}", Description = "Same as renewal_link (hosted bill when minted)." },
                    new TemplateVariableDto { Tag = "{{portal_magic_link}}", Description = "24h token on dunning/lifecycle only; digital delivery is the logged-out portal URL." }
                }
            },
            new TemplateVariableCategoryDto
            {
                Title = "Fulfillment Assets",
                Items = new List<TemplateVariableDto>
                {
                    new TemplateVariableDto { Tag = "{{fulfillment_url}}", Description = "First https fulfillment target on the product (not R2)." }
                }
            }
        };

        return Task.FromResult<IEnumerable<TemplateVariableCategoryDto>>(categories);
    }

    public async Task<bool> HasValidEmailConfigAsync(Guid tenantId)
    {
        var creds = await GetEmailConfigCredentialsAsync(tenantId);
        return creds is not null
            && creds.IsActive
            && !string.IsNullOrWhiteSpace(creds.SenderEmail)
            && !string.IsNullOrWhiteSpace(creds.ApiKey);
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

        if (!TenantEmailKey.TryResolve(_secretVault, result.ApiKey, out var plainKey))
        {
            return null;
        }

        return new TenantEmailCredentials(plainKey, result.SenderEmail, result.IsActive);
    }
}
