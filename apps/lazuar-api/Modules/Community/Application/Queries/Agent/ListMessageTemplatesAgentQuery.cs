// apps/lazuar-api/Modules/Community/Application/Queries/Agent/ListMessageTemplatesAgentQuery.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("List all message templates to find a Template ID.", "low", "SUPER_ADMIN", "ADMIN")]
public record ListMessageTemplatesAgentQuery(Guid OrganizationId) : IQuery<IEnumerable<AgentTemplateResult>>;

public record AgentTemplateResult(string TemplateId, string Name, string Channel, string Subject, string Body);

public class ListMessageTemplatesAgentQueryHandler : IQueryHandler<ListMessageTemplatesAgentQuery, IEnumerable<AgentTemplateResult>>
{
    private readonly IMessageTemplateQueryService _templateService;

    public ListMessageTemplatesAgentQueryHandler(IMessageTemplateQueryService templateService)
    {
        _templateService = templateService;
    }

    public async Task<IEnumerable<AgentTemplateResult>> Handle(ListMessageTemplatesAgentQuery request, CancellationToken cancellationToken)
    {
        var templates = await _templateService.GetAllTemplatesAsync(request.OrganizationId);
        
        return templates.Select(t => new AgentTemplateResult(
            t.Id, 
            t.Name, 
            t.Channel, 
            t.Subject, 
            t.Body)).ToList();
    }
}
