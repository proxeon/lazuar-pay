using System.Xml;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Services;

public interface IUblXmlGenerator
{
    XmlDocument GenerateInvoiceXml(SubmitDocumentRequestDto request, LhdnTenantConfig tenantConfig, string? originalUuid = null);
}
