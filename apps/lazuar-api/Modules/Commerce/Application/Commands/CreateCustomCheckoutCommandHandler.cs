using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.CRM.Contracts;
using Modules.Payments.Contracts.Queries;

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

        var now = DateTime.UtcNow;
        var expiresAt = request.ExpiresAt ?? now.AddDays(30);
        var dueAt = ResolveDueAt(now, request.DueAt, request.Terms);
        if (dueAt.HasValue)
        {
            var linkFloor = dueAt.Value.AddDays(14);
            if (expiresAt < linkFloor)
            {
                expiresAt = linkFloor;
            }
        }

        var domainLineItems = request.LineItems
            .Select(x => new AdHocLineItem(x.Description, x.Quantity, x.UnitPrice))
            .ToList();

        // Prefer explicit request gateway; otherwise first configured tenant gateway (null → resolved at checkout).
        var gatewayName = await ResolveGatewayPreferenceAsync(
            request.OrganizationId,
            request.GatewayName,
            ct);

        var session = new CheckoutSession(
            request.OrganizationId,
            clientProfileId,
            domainLineItems,
            expiresAt,
            request.IsB2bRequired,
            gatewayName,
            request.Currency
        );

        var quoteNumber = await _mediator.Send(
            new GenerateNextSequenceNumberCommand(request.OrganizationId, DocumentSeries.QuotePrefix()),
            ct);
        session.AssignDocumentNumber(quoteNumber);
        if (dueAt.HasValue)
        {
            session.SetDueAt(dueAt);
        }

        _repository.AddCheckoutSession(session);
        await _repository.SaveChangesAsync(ct);

        return session.Id;
    }

    private async Task<string?> ResolveGatewayPreferenceAsync(
        Guid organizationId,
        string? preferredGateway,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(preferredGateway))
        {
            return preferredGateway.Trim().ToUpperInvariant();
        }

        var configs = await _mediator.Send(new GetPaymentConfigQuery(organizationId), ct);
        var firstActive = configs.FirstOrDefault(c =>
            c.Is_active && (c.Has_api_key || c.Has_secret_key));

        return string.IsNullOrWhiteSpace(firstActive?.Gateway_type)
            ? null
            : firstActive.Gateway_type.Trim().ToUpperInvariant();
    }

    internal static DateTime? ResolveDueAt(DateTime now, DateTime? dueAt, string? terms)
    {
        if (dueAt.HasValue)
        {
            return dueAt.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dueAt.Value, DateTimeKind.Utc)
                : dueAt.Value.ToUniversalTime();
        }

        var normalized = (terms ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "due_on_receipt" => now,
            "net_7" => now.AddDays(7),
            "net_15" => now.AddDays(15),
            "net_30" => now.AddDays(30),
            _ => null
        };
    }
}
