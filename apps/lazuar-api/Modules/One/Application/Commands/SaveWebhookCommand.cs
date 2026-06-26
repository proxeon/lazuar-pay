// apps/lazuar-api/Modules/One/Application/Commands/SaveWebhookCommand.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

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
            endpoint.Update(request.Url, endpoint.SecretKey, request.IsActive);
        }

        await _repository.SaveChangesAsync(ct);
    }
}
