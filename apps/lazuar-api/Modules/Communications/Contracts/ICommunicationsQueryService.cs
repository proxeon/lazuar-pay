using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Communications.Contracts;

/// <summary>Decrypted BYOK credentials for internal dispatch only — never expose over HTTP.</summary>
public record TenantEmailCredentials(string ApiKey, string SenderEmail, bool IsActive);

public interface ICommunicationsQueryService
{
    Task<IEnumerable<MessageTemplateDto>> GetAllTemplatesAsync(Guid organizationId);
    Task<MessageTemplateDto?> GetTemplateByNameAsync(Guid organizationId, string name);
    Task<IEnumerable<TemplateVariableCategoryDto>> GetTemplateVariablesAsync();
    Task<bool> HasValidEmailConfigAsync(Guid tenantId);

    /// <summary>Masked config for admin GET (has_api_key + hint, never full key).</summary>
    Task<EmailConfigDto?> GetEmailConfigAsync(Guid tenantId);

    /// <summary>Decrypts stored key for message dispatch only.</summary>
    Task<TenantEmailCredentials?> GetEmailConfigCredentialsAsync(Guid tenantId);
}
