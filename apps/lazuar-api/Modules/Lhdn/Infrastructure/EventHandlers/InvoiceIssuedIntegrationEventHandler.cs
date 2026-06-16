using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Modules.Billing.Contracts.Events;
using Modules.Lhdn.Application.Commands;

namespace Modules.Lhdn.Infrastructure.EventHandlers;

public class InvoiceIssuedIntegrationEventHandler : IIntegrationEventHandler<InvoiceIssuedIntegrationEvent>
{
    private readonly IMediator _mediator;

    public InvoiceIssuedIntegrationEventHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task HandleAsync(InvoiceIssuedIntegrationEvent @event)
    {
        var payload = new SubmitDocumentRequestDto
        {
            Internal_id = @event.InvoiceNumber,
            Document_type = SubmitDocumentRequestDtoDocument_type._01,
            Issue_date = new DateTimeOffset(@event.IssueDate),
            Buyer_name = "Resolved via CRM",
            Buyer_tin = "C1234567890", // FIX: Changed from IG to C to match BRN format requirement
            Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
            Buyer_id_value = "202001012345",
            Buyer_address = new LhdnAddressDto 
            { 
                Line1 = "Address Line 1", 
                City = "Kuala Lumpur", 
                Postal_code = "50000", 
                State_code = LhdnAddressDtoState_code._14, 
                Country_code = "MYS" 
            },
            Items = new List<LhdnItemDto> 
            { 
                new LhdnItemDto 
                { 
                    Description = "Standard B2B Invoice", 
                    Classification_code = "022", 
                    Quantity = 1, 
                    Unit_price = (double)@event.Amount, 
                    Tax_rate = 0, 
                    Tax_amount = 0, 
                    Subtotal = (double)@event.Amount,
                    Tax_type_code = LhdnItemDtoTax_type_code._06
                } 
            },
            Total_excluding_tax = (double)@event.Amount,
            Total_tax = 0,
            Total_including_tax = (double)@event.Amount
        };

        var command = new SubmitTaxDocumentCommand(@event.OrganizationId, payload);
        await _mediator.Send(command);
    }
}
