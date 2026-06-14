using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace Modules.Lhdn.Application.Services;

public interface IXmlSignatureService
{
    void SignDocument(XmlDocument document, X509Certificate2 certificate);
}
