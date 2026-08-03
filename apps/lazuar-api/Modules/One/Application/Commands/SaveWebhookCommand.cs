// apps/lazuar-api/Modules/One/Application/Commands/SaveWebhookCommand.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record CreateWebhookEndpointResult(
    Guid Id,
    string Url,
    string SecretKey,
    bool IsActive,
    IReadOnlyList<string> EnabledEvents,
    DateTime CreatedAt);

public record CreateWebhookEndpointCommand(
    Guid OrganizationId,
    string Url,
    bool IsActive = true,
    IReadOnlyList<string>? EnabledEvents = null) : ICommand<CreateWebhookEndpointResult>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CreateWebhookEndpointCommandHandler : ICommandHandler<CreateWebhookEndpointCommand, CreateWebhookEndpointResult>
{
    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;

    public CreateWebhookEndpointCommandHandler(IOneRepository repository, ITokenGeneratorService tokenGenerator)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<CreateWebhookEndpointResult> Handle(CreateWebhookEndpointCommand request, CancellationToken ct)
    {
        var secret = "whsec_" + _tokenGenerator.GenerateSecureToken(24).PlainToken;
        var endpoint = new TenantWebhookEndpoint(
            request.OrganizationId,
            request.Url,
            secret,
            request.IsActive,
            request.EnabledEvents);

        _repository.AddWebhookEndpoint(endpoint);
        await _repository.SaveChangesAsync(ct);

        return new CreateWebhookEndpointResult(
            endpoint.Id,
            endpoint.Url,
            secret,
            endpoint.IsActive,
            endpoint.EnabledEvents.ToList(),
            endpoint.CreatedAt);
    }
}

public record UpdateWebhookEndpointCommand(
    Guid OrganizationId,
    Guid EndpointId,
    string Url,
    bool IsActive,
    IReadOnlyList<string>? EnabledEvents = null) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdateWebhookEndpointCommandHandler : ICommandHandler<UpdateWebhookEndpointCommand>
{
    private readonly IOneRepository _repository;

    public UpdateWebhookEndpointCommandHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateWebhookEndpointCommand request, CancellationToken ct)
    {
        var endpoint = await _repository.GetWebhookEndpointByIdAsync(request.EndpointId, ct)
            ?? throw new InvalidOperationException("Webhook endpoint not found.");

        if (endpoint.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Webhook endpoint not found.");
        }

        endpoint.Update(request.Url, request.IsActive, request.EnabledEvents);
        await _repository.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Legacy single-endpoint upsert retained for callers not yet migrated to multi-endpoint create/update.
/// Creates a new endpoint if none exist; otherwise updates the first endpoint for the org.
/// </summary>
public record SaveWebhookCommand(Guid OrganizationId, string Url, bool IsActive) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class SaveWebhookCommandHandler : ICommandHandler<SaveWebhookCommand>
{
    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;

    public SaveWebhookCommandHandler(IOneRepository repository, ITokenGeneratorService tokenGenerator)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task Handle(SaveWebhookCommand request, CancellationToken ct)
    {
        var endpoint = await _repository.GetWebhookEndpointAsync(request.OrganizationId, ct);

        if (endpoint == null)
        {
            var secret = "whsec_" + _tokenGenerator.GenerateSecureToken(24).PlainToken;
            endpoint = new TenantWebhookEndpoint(request.OrganizationId, request.Url, secret, request.IsActive);
            _repository.AddWebhookEndpoint(endpoint);
        }
        else
        {
            endpoint.Update(request.Url, request.IsActive);
        }

        await _repository.SaveChangesAsync(ct);
    }
}
