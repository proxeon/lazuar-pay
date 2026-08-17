using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;
using Modules.One.Contracts;

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
    private readonly ITenantWebhookRegistry _webhooks;

    public RegisterWebhookCommandHandler(ILhdnRepository repository, ITenantWebhookRegistry webhooks)
    {
        _repository = repository;
        _webhooks = webhooks;
    }

    public async Task<Guid> Handle(RegisterWebhookCommand request, CancellationToken ct)
    {
        var webhook = new WebhookSubscription(request.OrganizationId, request.Payload.Url, request.Payload.Secret);
        _repository.AddWebhookSubscription(webhook);
        await _repository.SaveChangesAsync(ct);

        var events = request.Payload.Events is { Count: > 0 }
            ? request.Payload.Events
            : LhdnWorkspaceWebhookEvents.InvoiceEvents.ToList();

        var live = await _webhooks.RegisterAsync(request.OrganizationId, request.Payload.Url, events, ct);
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
    private readonly ITenantWebhookRegistry _webhooks;

    public DeleteWebhookCommandHandler(ILhdnRepository repository, ITenantWebhookRegistry webhooks)
    {
        _repository = repository;
        _webhooks = webhooks;
    }

    public async Task Handle(DeleteWebhookCommand request, CancellationToken ct)
    {
        var live = await _webhooks.GetByIdAsync(request.OrganizationId, request.WebhookId, ct);
        if (live != null)
        {
            await _webhooks.DisableAsync(request.OrganizationId, live.Id, ct);

            var lhdnMatches = await _repository.GetActiveWebhooksAsync(request.OrganizationId, ct);
            foreach (var match in lhdnMatches.Where(w =>
                         string.Equals(w.Url, live.Url, StringComparison.Ordinal)))
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
            await _webhooks.DisableByUrlAsync(request.OrganizationId, target.Url, ct);
        }
    }
}
