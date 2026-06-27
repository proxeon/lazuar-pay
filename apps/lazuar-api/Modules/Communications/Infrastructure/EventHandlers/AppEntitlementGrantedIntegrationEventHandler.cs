using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Communications.Domain.Aggregates;
using Modules.One.Contracts;

namespace Modules.Communications.Infrastructure.EventHandlers;

public class AppEntitlementGrantedIntegrationEventHandler : IIntegrationEventHandler<AppEntitlementGrantedIntegrationEvent>
{
    private readonly CommunicationsDbContext _dbContext;

    public AppEntitlementGrantedIntegrationEventHandler(CommunicationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(AppEntitlementGrantedIntegrationEvent @event)
    {
        var hasTemplates = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .AnyAsync(t => t.OrganizationId == @event.TenantId);

        if (!hasTemplates)
        {
            var templates = new List<MessageTemplate>
            {
                new MessageTemplate(@event.TenantId, "Payment Failed", "ALL", 
                    "Action Needed: Payment Issue", 
                    "Hi {{customer_name}},\n\nWe tried to process your renewal, but the payment didn't go through. This usually just means your bank blocked the transaction or the card expired.\n\nTo ensure you don't lose access, please update your payment details here:\n\n[Securely Update Payment]({{renewal_link}})\n\nIf you need any help, just reply to this email.", 
                    "Hi {{customer_name}} 👋 Quick heads up: your recent card payment was declined by the bank. To keep your access active, you can quickly update your details here: {{renewal_link}}. Let us know if you need help!", 
                    true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}" }),
                    
                new MessageTemplate(@event.TenantId, "Subscription Cancelled", "ALL", 
                    "Your membership has ended", 
                    "Hi {{customer_name}},\n\nYour subscription has been cancelled.\n\nWe hope to see you again! 🙏", 
                    "Hi {{customer_name}}, your subscription has been cancelled. We hope to see you back soon! 🙏", 
                    true, System.Array.Empty<string>(), new[] { "{{customer_name}}" }),
                    
                new MessageTemplate(@event.TenantId, "Generic Receipt", "EMAIL", 
                    "Payment Receipt", 
                    "Hi {{customer_name}},\n\nThank you for your purchase. We have received your payment.", 
                    "Hi {{customer_name}}, thank you for your purchase. We have received your payment.", 
                    true, System.Array.Empty<string>(), new[] { "{{customer_name}}" })
            };

            _dbContext.MessageTemplates.AddRange(templates);
            await _dbContext.SaveChangesAsync();
        }
    }
}
