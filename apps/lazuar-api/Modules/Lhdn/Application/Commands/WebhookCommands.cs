using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Commands;

public record RegisterWebhookCommand(Guid OrganizationId, RegisterWebhookRequestDto Payload) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RegisterWebhookCommandHandler : ICommandHandler<RegisterWebhookCommand, Guid>
{
    private readonly ILhdnRepository _repository;

    public RegisterWebhookCommandHandler(ILhdnRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(RegisterWebhookCommand request, CancellationToken ct)
    {
        var webhook = new WebhookSubscription(request.OrganizationId, request.Payload.Url, request.Payload.Secret);
        
        _repository.AddWebhookSubscription(webhook);
        await _repository.SaveChangesAsync(ct);

        return webhook.Id;
    }
}

public record DeleteWebhookCommand(Guid OrganizationId, Guid WebhookId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class DeleteWebhookCommandHandler : ICommandHandler<DeleteWebhookCommand>
{
    private readonly ILhdnRepository _repository;

    public DeleteWebhookCommandHandler(ILhdnRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteWebhookCommand request, CancellationToken ct)
    {
        var webhooks = await _repository.GetActiveWebhooksAsync(request.OrganizationId, ct);
        var target = webhooks.FirstOrDefault(w => w.Id == request.WebhookId);

        if (target != null)
        {
            target.Deactivate();
            await _repository.SaveChangesAsync(ct);
        }
    }
}
