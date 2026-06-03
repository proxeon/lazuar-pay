namespace Modules.Messaging.Contracts;

public record MessageTemplateDto(Guid Id, string Name, string Subject, string Body);

public interface IMessageTemplateQueryService
{
    Task<IEnumerable<MessageTemplateDto>> GetTemplatesAsync(IEnumerable<Guid> templateIds);
    Task<MessageTemplateDto?> GetTemplateByNameAsync(Guid organizationId, string name);
}
