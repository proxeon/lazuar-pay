using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Commerce.Contracts.Commands;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Application.Commands;

public class AnonymizeSubscriberCommandHandler : ICommandHandler<AnonymizeSubscriberCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IMediator _mediator;

    public AnonymizeSubscriberCommandHandler(
        ICommerceRepository repository,
        ICrmQueryService crmQueryService,
        IMediator mediator)
    {
        _repository = repository;
        _crmQueryService = crmQueryService;
        _mediator = mediator;
    }

    public async Task Handle(AnonymizeSubscriberCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        var profile = await _crmQueryService.GetClientProfileAsync(subscription.ClientProfileId);
        if (profile == null)
        {
            throw new InvalidOperationException("Client profile not found.");
        }

        // Scrub logs while CRM still holds the real email, then anonymize CRM.
        if (!IsDummyAnonymizedEmail(profile.Email))
        {
            var logs = await _repository.GetTransactionLogsByCustomerEmailAsync(
                request.OrganizationId,
                profile.Email,
                ct);
            foreach (var log in logs)
            {
                log.Anonymize(subscription.ClientProfileId);
            }

            if (logs.Count > 0)
            {
                await _repository.SaveChangesAsync(ct);
            }
        }

        await _mediator.Send(
            new AnonymizeClientProfileCommand(request.OrganizationId, subscription.ClientProfileId),
            ct);
    }

    private static bool IsDummyAnonymizedEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.StartsWith("deleted_", StringComparison.OrdinalIgnoreCase)
        && email.EndsWith("@localhost", StringComparison.OrdinalIgnoreCase);
}
