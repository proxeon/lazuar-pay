using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Application.Commands;

public record UpdateLhdnCertificateCommand(Guid OrganizationId, string Base64P12, string Passphrase) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdateLhdnCertificateCommandHandler : ICommandHandler<UpdateLhdnCertificateCommand>
{
    private readonly ILhdnRepository _repository;
    private readonly ICertificateVaultService _vaultService;

    public UpdateLhdnCertificateCommandHandler(ILhdnRepository repository, ICertificateVaultService vaultService)
    {
        _repository = repository;
        _vaultService = vaultService;
    }

    public async Task Handle(UpdateLhdnCertificateCommand request, CancellationToken ct)
    {
        var config = await _repository.GetTenantConfigAsync(request.OrganizationId, ct);
        if (config == null)
        {
            throw new InvalidOperationException("LHDN Tenant Configuration is missing.");
        }

        var (encryptedPfx, cipherText) = _vaultService.EncryptCertificate(request.Base64P12, request.Passphrase);
        
        config.UpdateCertificate(encryptedPfx, cipherText);
        
        await _repository.SaveChangesAsync(ct);
    }
}
