using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Ports;
using Modules.Payments.Application.Services;
using Modules.Payments.Contracts.Commands;
using Modules.Payments.Contracts.Queries;

namespace Modules.Payments.Application.Queries;

public class GetIntegrationCheckoutQueryHandler
    : IQueryHandler<GetIntegrationCheckoutQuery, IntegrationCheckoutResult?>
{
    private readonly IIntegrationCheckoutSessionRepository _sessions;

    public GetIntegrationCheckoutQueryHandler(IIntegrationCheckoutSessionRepository sessions)
    {
        _sessions = sessions;
    }

    public async Task<IntegrationCheckoutResult?> Handle(
        GetIntegrationCheckoutQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(
            request.OrganizationId, request.CheckoutId, cancellationToken);
        if (session == null)
            return null;

        if (session.TryExpireIfPast(DateTime.UtcNow))
        {
            await _sessions.SaveChangesAsync(cancellationToken);
        }

        return new IntegrationCheckoutResult(
            session.Id,
            session.CheckoutUrl,
            session.GatewayName,
            session.Status,
            session.Amount,
            session.Currency,
            session.ProviderSessionId,
            session.GatewayTransactionId,
            session.ExpiresAt,
            IntegrationCheckoutMetadata.Deserialize(session.MetadataJson));
    }
}
