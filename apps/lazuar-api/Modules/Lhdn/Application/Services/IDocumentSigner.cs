using System.Security.Cryptography.X509Certificates;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Services;

public record SignedUblDocument(string Content, string HashHex, string Format, string DocumentVersion);

public interface IDocumentSigner
{
    bool CanSign(LhdnTenantConfig config);

    SignedUblDocument SignJson(
        SubmitDocumentRequestDto request,
        LhdnTenantConfig config,
        X509Certificate2 certificate,
        string? supplierSstNumber);
}
