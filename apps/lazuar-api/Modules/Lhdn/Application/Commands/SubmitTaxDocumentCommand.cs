using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Commands;

public record SubmitTaxDocumentCommand(Guid OrganizationId, SubmitDocumentRequestDto Payload) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class SubmitTaxDocumentCommandHandler : ICommandHandler<SubmitTaxDocumentCommand, Guid>
{
    private readonly ILhdnRepository _repository;
    private readonly IDocumentStrategyFactory _strategyFactory;
    private readonly IJsonSignatureService _signatureService;
    private readonly ICertificateVaultService _vaultService;

    public SubmitTaxDocumentCommandHandler(
        ILhdnRepository repository, 
        IDocumentStrategyFactory strategyFactory,
        IJsonSignatureService signatureService,
        ICertificateVaultService vaultService)
    {
        _repository = repository;
        _strategyFactory = strategyFactory;
        _signatureService = signatureService;
        _vaultService = vaultService;
    }

    public async Task<Guid> Handle(SubmitTaxDocumentCommand request, CancellationToken ct)
    {
        var config = await _repository.GetTenantConfigAsync(request.OrganizationId, ct);
        if (config == null)
        {
            throw new InvalidOperationException("LHDN Tenant Configuration is missing.");
        }

        var documentVersion = string.IsNullOrWhiteSpace(request.Payload.Document_version) ? "1.0" : request.Payload.Document_version;

        var strategy = _strategyFactory.GetStrategy(request.Payload);
        
        // Treat as a generic object so Application layer doesn't depend on Infrastructure models
        var jsonDocument = strategy.Generate(request.Payload, config, documentVersion);

        string finalJsonString;
        string documentHashHex;

        if (documentVersion == "1.1")
        {
            if (string.IsNullOrEmpty(config.EncryptedPfxBase64) || string.IsNullOrEmpty(config.PfxPasswordCiphertext))
            {
                throw new InvalidOperationException("Certificate missing for v1.1 document. Please upload a valid PKCS#12 certificate.");
            }

            using var cert = _vaultService.GetDecryptedCertificate(config.EncryptedPfxBase64, config.PfxPasswordCiphertext);
            
            var signingResult = _signatureService.SignDocument(jsonDocument, cert);
            finalJsonString = signingResult.FinalJsonString;
            documentHashHex = signingResult.HexDigest;
        }
        else
        {
            var serializedResult = _signatureService.SerializeUnsignedDocument(jsonDocument);
            finalJsonString = serializedResult.JsonString;
            documentHashHex = serializedResult.DocumentHashHex;
        }

        var taxDocument = new TaxDocument(
            request.OrganizationId,
            request.Payload.Internal_id,
            documentHashHex,
            finalJsonString 
        );

        _repository.AddTaxDocument(taxDocument);
        await _repository.SaveChangesAsync(ct);

        return taxDocument.Id;
    }
}
