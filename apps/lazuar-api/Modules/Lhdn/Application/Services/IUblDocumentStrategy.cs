using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Services;

/// <summary>
/// Strategy interface to map flat DTOs into the LHDN proprietary JSON structure.
/// Returns a base object to prevent leaking Infrastructure models into the Application layer.
/// </summary>
public interface IUblDocumentStrategy
{
    object Generate(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion);
}
