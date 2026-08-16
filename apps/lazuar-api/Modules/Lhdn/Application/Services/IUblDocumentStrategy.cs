using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Services;

public interface IUblDocumentStrategy
{
    string Generate(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion, string? supplierSstNumber = null);
}
