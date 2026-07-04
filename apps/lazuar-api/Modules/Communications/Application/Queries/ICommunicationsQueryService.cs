using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Communications.Application.Queries;

public interface ICommunicationsQueryService
{
    Task<IEnumerable<MessageTemplateDto>> GetAllTemplatesAsync(Guid organizationId);
    Task<MessageTemplateDto?> GetTemplateByNameAsync(Guid organizationId, string name);
    Task<IEnumerable<TemplateVariableCategoryDto>> GetTemplateVariablesAsync();
    Task<bool> HasValidEmailConfigAsync(Guid tenantId);
    Task<EmailConfigDto?> GetEmailConfigAsync(Guid tenantId);
}
