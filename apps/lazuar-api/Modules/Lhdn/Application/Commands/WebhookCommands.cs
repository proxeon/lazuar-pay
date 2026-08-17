using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;
using Modules.One.Application;
using Modules.One.Application.Commands;

namespace Modules.Lhdn.Application.Commands;

public static class LhdnWorkspaceWebhookEvents
{
    public static readonly string[] InvoiceEvents = ["invoice.valid", "invoice.invalid"];
}

public record RegisterWebhookCommand(Guid OrganizationId, RegisterWebhookRequestDto Payload) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RegisterWebhookCommandHandler : ICommandHandler<RegisterWebhookCommand, Guid>
{
    private readonly ILhdnRepository _repository;
    private readonly IMediator _mediator;

    public RegisterWebhookCommandHandler(ILhdnRepository repository, IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async Task<Guid> Handle(RegisterWebhookCommand request, CancellationToken ct)
    {
        var webhook = new WebhookSubscription(request.OrganizationId, request.Payload.Url, request.Payload.Secret);
        _repository.AddWebhookSubscription(webhook);
        await _repository.SaveChangesAsync(ct);

        var events = request.Payload.Events is { Count: > 0 }
            ? request.Payload.Events
            : LhdnWorkspaceWebhookEvents.InvoiceEvents.ToList();

        var live = await _mediator.Send(
            new CreateWebhookEndpointCommand(
                request.OrganizationId,
                request.Payload.Url,
                IsActive: true,
                EnabledEvents: events),
            ct);

        return live.Id;
    }
}

public record DeleteWebhookCommand(Guid OrganizationId, Guid WebhookId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class DeleteWebhookCommandHandler : ICommandHandler<DeleteWebhookCommand>
{
    private readonly ILhdnRepository _repository;
    private readonly IOneRepository _one;

    public DeleteWebhookCommandHandler(ILhdnRepository repository, IOneRepository one)
    {
        _repository = repository;
        _one = one;
    }

    public async Task Handle(DeleteWebhookCommand request, CancellationToken ct)
    {
        var oneEndpoint = await _one.GetWebhookEndpointByIdAsync(request.WebhookId, ct);
        if (oneEndpoint != null && oneEndpoint.OrganizationId == request.OrganizationId)
        {
            oneEndpoint.Disable();
            await _one.SaveChangesAsync(ct);

            var lhdnMatches = await _repository.GetActiveWebhooksAsync(request.OrganizationId, ct);
            foreach (var match in lhdnMatches.Where(w =>
                         string.Equals(w.Url, oneEndpoint.Url, StringComparison.Ordinal)))
            {
                match.Deactivate();
            }

            await _repository.SaveChangesAsync(ct);
            return;
        }

        var webhooks = await _repository.GetActiveWebhooksAsync(request.OrganizationId, ct);
        var target = webhooks.FirstOrDefault(w => w.Id == request.WebhookId);

        if (target != null)
        {
            target.Deactivate();
            await _repository.SaveChangesAsync(ct);

            var ones = await _one.ListWebhookEndpointsAsync(request.OrganizationId, ct);
            var live = ones.FirstOrDefault(e =>
                e.IsActive && string.Equals(e.Url, target.Url, StringComparison.Ordinal));
            if (live != null)
            {
                live.Disable();
                await _one.SaveChangesAsync(ct);
            }
        }
    }
}
