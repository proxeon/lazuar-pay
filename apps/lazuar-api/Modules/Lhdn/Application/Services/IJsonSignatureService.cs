using System.Security.Cryptography.X509Certificates;

namespace Modules.Lhdn.Application.Services;

public record JsonSigningResult(
    string FinalJsonString,
    string Base64Digest,
    string HexDigest,
    string SignatureValue
);

public interface IJsonSignatureService
{
    JsonSigningResult SignDocument(object document, X509Certificate2 certificate);
    (string JsonString, string DocumentHashHex) SerializeUnsignedDocument(object document);
}
