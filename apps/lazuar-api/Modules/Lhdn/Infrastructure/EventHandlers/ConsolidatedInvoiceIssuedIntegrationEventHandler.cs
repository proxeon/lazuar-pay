using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Modules.Billing.Contracts.Events;
using Modules.Lhdn.Application.Commands;

namespace Modules.Lhdn.Infrastructure.EventHandlers;

public class ConsolidatedInvoiceIssuedIntegrationEventHandler : IIntegrationEventHandler<ConsolidatedInvoiceIssuedIntegrationEvent>
{
    private readonly IMediator _mediator;

    public ConsolidatedInvoiceIssuedIntegrationEventHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task HandleAsync(ConsolidatedInvoiceIssuedIntegrationEvent @event)
    {
        var payload = new SubmitDocumentRequestDto
        {
            Internal_id = @event.InternalReferenceId,
            Document_type = SubmitDocumentRequestDtoDocument_type._01,
            Issue_date = new DateTimeOffset(@event.IssueDate),
            Buyer_name = "General Public",
            Buyer_tin = "EI00000000010",
            Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
            Buyer_id_value = "NA",
            Buyer_address = new LhdnAddressDto
            {
                Line1 = "NA",
                City = "NA",
                Postal_code = "00000",
                State_code = LhdnAddressDtoState_code._17,
                Country_code = "MYS"
            },
            Items = @event.Items.Select(i => new LhdnItemDto
            {
                Description = i.Description,
                Classification_code = i.ClassificationCode,
                Quantity = (double)i.Quantity,
                Unit_price = (double)i.UnitPrice,
                Tax_rate = (double)i.TaxRate,
                Tax_amount = (double)i.TaxAmount,
                Subtotal = (double)i.Subtotal,
                Tax_type_code = Enum.Parse<LhdnItemDtoTax_type_code>("_" + i.TaxTypeCode)
            }).ToList(),
            Total_excluding_tax = (double)@event.TotalExcludingTax,
            Total_tax = (double)@event.TotalTax,
            Total_including_tax = (double)@event.TotalIncludingTax
        };

        // Added dynamic idempotency key generation for internal system events
        var idempotencyKey = Guid.CreateVersion7().ToString();
        var command = new SubmitTaxDocumentCommand(@event.OrganizationId, idempotencyKey, payload);
        
        await _mediator.Send(command);
    }
}
