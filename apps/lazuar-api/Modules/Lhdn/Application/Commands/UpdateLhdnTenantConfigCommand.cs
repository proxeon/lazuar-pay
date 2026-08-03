using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Commands;

public record UpdateLhdnTenantConfigCommand(Guid OrganizationId, UpdateLhdnTenantConfigRequestDto Request) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdateLhdnTenantConfigCommandHandler : ICommandHandler<UpdateLhdnTenantConfigCommand>
{
    private readonly ILhdnRepository _repository;

    public UpdateLhdnTenantConfigCommandHandler(ILhdnRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateLhdnTenantConfigCommand request, CancellationToken ct)
    {
        var body = request.Request;
        var environment = body.Environment.ToString();
        var intermediary = body.Intermediary_mode ?? false;

        var config = await _repository.GetTenantConfigAsync(request.OrganizationId, ct);
        if (config == null)
        {
            config = new LhdnTenantConfig(
                request.OrganizationId,
                intermediary,
                body.Supplier_tin,
                body.Id_type,
                body.Id_value,
                environment,
                body.Msic_code);
            _repository.AddTenantConfig(config);
        }
        else
        {
            config.UpdateProfile(
                body.Supplier_tin,
                body.Id_type,
                body.Id_value,
                environment,
                body.Msic_code,
                intermediary);
        }

        config.UpdateLegalAddress(
            body.Legal_name,
            body.Address_line1,
            body.City,
            body.State,
            body.Postal,
            body.Country);

        config.UpdateApiCredentialsPreserveSecret(body.Myinvois_client_id, body.Myinvois_client_secret);

        await _repository.SaveChangesAsync(ct);
    }
}
