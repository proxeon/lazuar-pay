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
    private readonly IUblXmlGenerator _xmlGenerator;

    public SubmitTaxDocumentCommandHandler(ILhdnRepository repository, IUblXmlGenerator xmlGenerator)
    {
        _repository = repository;
        _xmlGenerator = xmlGenerator;
    }

    public async Task<Guid> Handle(SubmitTaxDocumentCommand request, CancellationToken ct)
    {
        var xmlDoc = _xmlGenerator.GenerateInvoiceXml(request.Payload);
        var rawXmlString = xmlDoc.OuterXml;

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawXmlString));
        var documentHash = Convert.ToBase64String(hashBytes);

        var taxDocument = new TaxDocument(
            request.OrganizationId,
            request.Payload.Internal_id,
            documentHash,
            rawXmlString 
        );

        _repository.AddTaxDocument(taxDocument);
        await _repository.SaveChangesAsync(ct);

        return taxDocument.Id;
    }
}
