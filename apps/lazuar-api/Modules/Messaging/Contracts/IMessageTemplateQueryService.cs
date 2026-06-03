namespace Modules.Messaging.Contracts;

public record MessageTemplateDto(Guid Id, string Name);

public interface IMessageTemplateQueryService
{
    Task<IEnumerable<MessageTemplateDto>> GetTemplatesAsync(IEnumerable<Guid> templateIds);
}
