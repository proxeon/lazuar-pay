using System.Security.Cryptography.X509Certificates;
using Modules.Lhdn.Infrastructure.Models;

namespace Modules.Lhdn.Application.Services;

public record JsonSigningResult(
    LhdnJsonDocument SignedPayload,
    string Base64Digest,
    string HexDigest,
    string SignatureValue
);

public interface IJsonSignatureService
{
    JsonSigningResult SignDocument(LhdnJsonDocument document, X509Certificate2 certificate);
}
