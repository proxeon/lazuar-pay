using System.Security.Cryptography.X509Certificates;

namespace Modules.Lhdn.Application.Services;

public record JsonSigningResult(
    string FinalJsonString,
    string HexDigest,
    string SignatureValue
);

public interface IJsonSignatureService
{
    JsonSigningResult SignDocument(string rawXml, X509Certificate2 certificate);
    
    // Dummy method restored to satisfy legacy test mocks without triggering Obsolete warnings.
    (string JsonString, string DocumentHashHex) SerializeUnsignedDocument(object document);
}
