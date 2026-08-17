using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts.Events;
using Modules.Commerce.Contracts;
using Modules.CRM.Contracts;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Infrastructure.Services;

namespace Modules.Lhdn.Infrastructure.EventHandlers;

public class B2bTaxInvoiceRequestedIntegrationEventHandler : IIntegrationEventHandler<B2bTaxInvoiceRequestedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ICommerceDocumentLookup _commerceDocumentLookup;
    private readonly ICrmQueryService _crmQueryService;
    private readonly ILogger<B2bTaxInvoiceRequestedIntegrationEventHandler> _logger;

    public B2bTaxInvoiceRequestedIntegrationEventHandler(
        IMediator mediator,
        ICommerceDocumentLookup commerceDocumentLookup,
        ICrmQueryService crmQueryService,
        ILogger<B2bTaxInvoiceRequestedIntegrationEventHandler> logger)
    {
        _mediator = mediator;
        _commerceDocumentLookup = commerceDocumentLookup;
        _crmQueryService = crmQueryService;
        _logger = logger;
    }

    public async Task HandleAsync(B2bTaxInvoiceRequestedIntegrationEvent @event)
    {
        var customer = await _commerceDocumentLookup.GetCustomerForDocumentAsync(
            @event.OrganizationId,
            @event.GatewayTransactionId,
            @event.CorrelationId);

        ClientProfileDto? profile = null;
        if (!string.IsNullOrWhiteSpace(customer?.Email))
        {
            profile = await _crmQueryService.GetClientProfileByEmailAsync(@event.OrganizationId, customer.Email);
        }

        if (!LhdnBuyerMapper.TryCreatePayloadBuyer(
                profile,
                customer,
                out var buyerName,
                out var buyerTin,
                out var idType,
                out var idValue,
                out var address))
        {
            _logger.LogInformation(
                "Skipping MyInvois type 01 for invoice {InvoiceNumber}: no real buyer TIN on CRM.",
                @event.InvoiceNumber);
            return;
        }

        var payload = new SubmitDocumentRequestDto
        {
            Internal_id = @event.InvoiceNumber,
            Document_type = SubmitDocumentRequestDtoDocument_type._01,
            Issue_date = DateTimeOffset.UtcNow,
            Buyer_name = buyerName,
            Buyer_tin = buyerTin,
            Buyer_id_type = idType,
            Buyer_id_value = idValue,
            Buyer_email = customer?.Email,
            Buyer_address = address,
            Items = new List<LhdnItemDto>
            {
                new()
                {
                    Description = string.IsNullOrWhiteSpace(@event.LineDescription)
                        ? "Sale"
                        : @event.LineDescription,
                    Classification_code = "022",
                    Quantity = 1,
                    Unit_price = (double)@event.AmountExcludingTax,
                    Tax_rate = @event.AmountExcludingTax == 0
                        ? 0
                        : (double)Math.Round((@event.TaxAmount / @event.AmountExcludingTax) * 100m, 2),
                    Tax_amount = (double)@event.TaxAmount,
                    Subtotal = (double)@event.AmountExcludingTax,
                    Tax_type_code = @event.TaxAmount > 0
                        ? LhdnItemDtoTax_type_code._02
                        : LhdnItemDtoTax_type_code._06
                }
            },
            Total_excluding_tax = (double)@event.AmountExcludingTax,
            Total_tax = (double)@event.TaxAmount,
            Total_including_tax = (double)(@event.AmountExcludingTax + @event.TaxAmount)
        };

        var idempotencyKey = $"b2b-inv:{@event.OrganizationId:N}:{@event.InvoiceNumber}";
        await _mediator.Send(new SubmitTaxDocumentCommand(@event.OrganizationId, idempotencyKey, payload));
    }
}
