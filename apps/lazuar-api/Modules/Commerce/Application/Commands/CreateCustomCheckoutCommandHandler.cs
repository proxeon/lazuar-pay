using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Application.Commands;

public class CreateCustomCheckoutCommandHandler : ICommandHandler<CreateCustomCheckoutCommand, Guid>
{
    private readonly ICommerceRepository _repository;
    private readonly IMediator _mediator;

    public CreateCustomCheckoutCommandHandler(ICommerceRepository repository, IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async Task<Guid> Handle(CreateCustomCheckoutCommand request, CancellationToken ct)
    {
        var resolveCrmProfileCmd = new ResolveClientProfileCommand(
            request.OrganizationId,
            request.ClientName,
            request.ClientEmail,
            ""
        );

        var clientProfileId = await _mediator.Send(resolveCrmProfileCmd, ct);

        var expiresAt = request.ExpiresAt ?? DateTime.UtcNow.AddDays(30);

        var domainLineItems = request.LineItems
            .Select(x => new AdHocLineItem(x.Description, x.Quantity, x.UnitPrice))
            .ToList();

        var session = new CheckoutSession(
            request.OrganizationId,
            clientProfileId,
            domainLineItems,
            expiresAt,
            request.IsB2bRequired
        );

        _repository.AddCheckoutSession(session);
        await _repository.SaveChangesAsync(ct);

        return session.Id;
    }
}
