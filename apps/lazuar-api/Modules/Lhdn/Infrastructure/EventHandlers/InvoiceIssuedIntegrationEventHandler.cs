using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Modules.Billing.Contracts.Events;
using Modules.Lhdn.Application.Commands;

namespace Modules.Lhdn.Infrastructure.EventHandlers;

/// <summary>
/// Listens for finalized invoices from the Billing module and maps them into LHDN submission commands.
/// Transforms internal billing concepts into the strict UBL API contract.
/// </summary>
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
            Document_type = "01",
            Issue_date = new DateTimeOffset(@event.IssueDate),
            Buyer_name = "Resolved via CRM",
            Buyer_tin = "IG1234567890", 
            Buyer_id_type = "BRN",
            Buyer_id_value = "202001012345",
            Buyer_address = new LhdnAddressDto 
            { 
                Line1 = "Address Line 1", 
                City = "Kuala Lumpur", 
                Postal_code = "50000", 
                State_code = "14", 
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
                    Subtotal = (double)@event.Amount 
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
