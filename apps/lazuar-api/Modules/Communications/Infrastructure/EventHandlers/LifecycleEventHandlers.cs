using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Events;
using Modules.Communications.Application;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;
using Modules.One.Contracts;

namespace Modules.Communications.Infrastructure.EventHandlers;

public class LifecycleEventHandlers : IIntegrationEventHandler<SubscriptionCanceledIntegrationEvent>
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IOneQueryService _oneQueryService;
    private readonly ISubscriberQueryService _subscriberQueryService;
    private readonly IMagicLinkTokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IEventBus _eventBus;

    public LifecycleEventHandlers(
        CommunicationsDbContext dbContext,
        ICrmQueryService crmQueryService,
        IOneQueryService oneQueryService,
        ISubscriberQueryService subscriberQueryService,
        IMagicLinkTokenService tokenService,
        IConfiguration configuration,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _crmQueryService = crmQueryService;
        _oneQueryService = oneQueryService;
        _subscriberQueryService = subscriberQueryService;
        _tokenService = tokenService;
        _configuration = configuration;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(SubscriptionCanceledIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.OrganizationId, @event.ClientProfileId);
        var toEmail = profile?.Email;
        if (profile == null || string.IsNullOrEmpty(toEmail)) return;

        var template = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.OrganizationId == @event.OrganizationId && t.Name == "Subscription Cancelled");

        if (template == null) return;

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(@event.OrganizationId);
        var mail = await _subscriberQueryService.GetSubscriptionMailContextAsync(
            @event.OrganizationId, @event.SubscriptionId);

        var portalBase = (_configuration["App:ClientUrl"] ?? "https://portal.lazuar.com").TrimEnd('/');
        var token = _tokenService.GenerateToken(@event.SubscriptionId);
        var links = MessageLinkBuilder.Build(portalBase, workspace?.Slug ?? "", @event.SubscriptionId.ToString(), token);
        var amount = mail == null ? "" : MessageTemplateHydrator.FormatMoney(mail.Price);

        var ctx = new MessageTemplateContext(
            CustomerName: string.IsNullOrWhiteSpace(profile.Full_name) ? "Customer" : profile.Full_name,
            CustomerEmail: toEmail,
            CustomerPhone: profile.Phone ?? "",
            BusinessName: string.IsNullOrWhiteSpace(workspace?.Name) ? "Lazuar Merchant" : workspace.Name,
            PlanName: mail?.PlanName ?? "",
            Amount: amount,
            TotalPrice: amount,
            Currency: mail?.Currency ?? "",
            DaysOverdue: "",
            CurrentPeriodEnd: MessageTemplateHydrator.FormatPeriodEnd(mail?.NextBillingDate),
            RenewalLink: links.RenewalLink,
            PortalMagicLink: links.PortalMagicLink,
            UpdatePaymentLink: links.UpdatePaymentLink);

        var whatsapp = MessageTemplateHydrator.Populate(template.WhatsAppBody, ctx);

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            @event.OrganizationId,
            toEmail,
            profile.Phone,
            MessageTemplateHydrator.Populate(template.Subject, ctx),
            MarkdownParser.ToHtml(MessageTemplateHydrator.PopulateHtml(template.EmailBody, ctx)),
            string.IsNullOrEmpty(whatsapp) ? null : whatsapp,
            template.Channel
        ));
        await _dbContext.SaveChangesAsync();
    }
}
