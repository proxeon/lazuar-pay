using System.Xml;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Services;

public interface IUblDocumentStrategy
{
    XmlDocument Generate(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion);
}
