using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Modules.Messaging.Contracts;

public record MessageTemplateDto(Guid Id, string Name, string Subject, string Body, bool IsDefault, DateTime UpdatedAt);

public interface IMessageTemplateQueryService {
    Task < IEnumerable < MessageTemplateDto >> GetTemplatesAsync(IEnumerable < Guid > templateIds);
    Task < MessageTemplateDto ? > GetTemplateByNameAsync(Guid organizationId, string name);
    Task < IEnumerable < MessageTemplateDto >> GetAllTemplatesAsync(Guid organizationId); // NEW
}
