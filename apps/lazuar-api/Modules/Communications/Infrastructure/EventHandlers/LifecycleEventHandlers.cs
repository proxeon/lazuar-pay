using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Communications.Infrastructure.EventHandlers;

public class LifecycleEventHandlers : IIntegrationEventHandler<SubscriptionCanceledIntegrationEvent>
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IEventBus _eventBus;

    public LifecycleEventHandlers(
        CommunicationsDbContext dbContext,
        ICrmQueryService crmQueryService,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _crmQueryService = crmQueryService;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(SubscriptionCanceledIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var template = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.OrganizationId == @event.OrganizationId && t.Name == "Subscription Cancelled");

        if (template != null)
        {
            var body = template.EmailBody
                .Replace("{{customer_name}}", profile.Full_name, StringComparison.OrdinalIgnoreCase);

            await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
                @event.OrganizationId,
                profile.Email,
                profile.Phone,
                template.Subject,
                MarkdownParser.ToHtml(body),
                null,
                template.Channel
            ));
            await _dbContext.SaveChangesAsync();
        }
    }
}
