using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Messaging.Contracts;

public interface IMessageTemplateQueryService 
{
    Task<IEnumerable<MessageTemplateDto>> GetTemplatesAsync(IEnumerable<Guid> templateIds);
    Task<MessageTemplateDto?> GetTemplateByNameAsync(Guid organizationId, string name);
    Task<IEnumerable<MessageTemplateDto>> GetAllTemplatesAsync(Guid organizationId);
}
