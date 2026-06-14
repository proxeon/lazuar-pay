using System.Xml;
using Lazuar.ApiTypes;

namespace Modules.Lhdn.Application.Services;

public interface IUblXmlGenerator
{
    XmlDocument GenerateInvoiceXml(SubmitDocumentRequestDto request);
}
