using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Services.Strategies;

namespace Modules.Lhdn.Infrastructure.Services;

/// <summary>
/// MyInvois JSON UBL 1.1 signer. Hashes the unsigned JSON (no UBLExtensions), RSA-SHA256 signs,
/// then appends a signature object. XML XAdES is not used — LHDN's XML-DSig path is known-broken.
/// </summary>
public sealed class JsonUblDocumentSigner : IDocumentSigner
{
    public bool CanSign(LhdnTenantConfig config) =>
        !string.IsNullOrWhiteSpace(config.EncryptedPfxBase64)
        && !string.IsNullOrWhiteSpace(config.PfxPasswordCiphertext);

    public SignedUblDocument SignJson(
        SubmitDocumentRequestDto request,
        LhdnTenantConfig config,
        X509Certificate2 certificate,
        string? supplierSstNumber)
    {
        var view = ViewModelMapper.MapToViewModel(request, config, "1.1", supplierSstNumber);
        var unsigned = UblJsonDocumentBuilder.Build(view);
        var unsignedJson = unsigned.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var unsignedBytes = Encoding.UTF8.GetBytes(unsignedJson);

        var rsa = certificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Signing certificate does not contain an RSA private key.");

        var signature = rsa.SignData(unsignedBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var hashHex = Convert.ToHexString(SHA256.HashData(unsignedBytes)).ToLowerInvariant();
        var certDer = certificate.Export(X509ContentType.Cert);

        var invoice = unsigned["Invoice"]?[0] as JsonObject
            ?? throw new InvalidOperationException("Unsigned JSON is missing Invoice[0].");
        invoice["UBLExtensions"] = UblJsonDocumentBuilder.BuildSignatureExtensions(
            Convert.ToBase64String(signature),
            Convert.ToBase64String(certDer),
            hashHex);

        var signedJson = unsigned.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var signedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signedJson))).ToLowerInvariant();
        return new SignedUblDocument(signedJson, signedHash, "JSON", "1.1");
    }
}
