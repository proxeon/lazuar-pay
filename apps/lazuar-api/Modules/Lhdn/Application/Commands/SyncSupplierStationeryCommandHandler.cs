using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Contracts;

namespace Modules.Lhdn.Application.Commands;

public class SyncSupplierStationeryCommandHandler : ICommandHandler<SyncSupplierStationeryCommand>
{
    private readonly ILhdnRepository _repository;

    public SyncSupplierStationeryCommandHandler(ILhdnRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(SyncSupplierStationeryCommand request, CancellationToken ct)
    {
        var config = await _repository.GetTenantConfigAsync(request.OrganizationId, ct);
        if (config == null)
        {
            return;
        }

        config.SyncStationeryIdentity(
            request.LegalName,
            request.Tin,
            request.AddressLine1,
            request.City,
            request.State,
            request.Postal,
            request.Country);

        await _repository.SaveChangesAsync(ct);
    }
}
