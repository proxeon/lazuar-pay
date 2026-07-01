using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.One.Infrastructure;

namespace Modules.One.Application.Commands;

public record RetryWebhookDeliveryCommand(Guid OrganizationId, Guid LogId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RetryWebhookDeliveryCommandHandler : ICommandHandler<RetryWebhookDeliveryCommand>
{
    private readonly OneDbContext _dbContext;

    public RetryWebhookDeliveryCommandHandler(OneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(RetryWebhookDeliveryCommand request, CancellationToken ct)
    {
        var log = await _dbContext.WebhookDeliveryOutboxes
            .FirstOrDefaultAsync(l => l.Id == request.LogId && l.OrganizationId == request.OrganizationId, ct);

        if (log == null)
        {
            throw new InvalidOperationException("Webhook log not found.");
        }

        if (log.Status == "SUCCESS")
        {
            throw new InvalidOperationException("Cannot retry a successfully delivered webhook.");
        }

        log.ResetForRetry();

        await _dbContext.SaveChangesAsync(ct);
    }
}
