using System;
using System.Security.Cryptography;
using System.Text;
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

    public SubmitTaxDocumentCommandHandler(
        ILhdnRepository repository, 
        IDocumentStrategyFactory strategyFactory)
    {
        _repository = repository;
        _strategyFactory = strategyFactory;
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
        
        var rawXmlString = strategy.Generate(request.Payload, config, documentVersion);

        var normalizedXmlString = rawXmlString.Replace("\r\n", "\n");
        var xmlBytes = Encoding.UTF8.GetBytes(normalizedXmlString);
        
        var hashBytes = SHA256.HashData(xmlBytes);
        var documentHashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var taxDocument = new TaxDocument(
            request.OrganizationId,
            request.Payload.Internal_id,
            documentHashHex,
            normalizedXmlString 
        );

        _repository.AddTaxDocument(taxDocument);
        await _repository.SaveChangesAsync(ct);

        return taxDocument.Id;
    }
}
