using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Infrastructure.Models;
using Modules.Lhdn.Infrastructure.Serialization;

namespace Modules.Lhdn.Infrastructure.Services;

public class JsonSignatureService : IJsonSignatureService
{
    private const string ExtensionUri = "urn:oasis:names:specification:ubl:dsig:enveloped:xades";
    private const string SignatureId = "urn:oasis:names:specification:ubl:signature:1";
    private const string ReferencedSignatureId = "urn:oasis:names:specification:ubl:signature:Invoice";
    private const string SignatureMethodAlgo = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string DigestMethodAlgo = "http://www.w3.org/2001/04/xmlenc#sha256";

    public JsonSigningResult SignDocument(LhdnJsonDocument document, X509Certificate2 certificate)
    {
        var signingTime = DateTime.UtcNow;

        // Step 1: Hash the canonical document directly from a stream to avoid LOH string allocations.
        var documentHashBytes = HashDocument(document);
        var documentBase64Digest = Convert.ToBase64String(documentHashBytes);
        var documentHexDigest = Convert.ToHexString(documentHashBytes).ToLowerInvariant();

        // Step 2: Hash the X509 Certificate (DER encoded)
        var certDigestBase64 = Convert.ToBase64String(SHA256.HashData(certificate.RawData));

        var certContent = Convert.ToBase64String(certificate.RawData);
        var issuerName = ParseDistinguishedName(certificate.Issuer);
        var subjectName = ParseDistinguishedName(certificate.Subject);
        var serialNumber = ParseSerialNumber(certificate.SerialNumber);
        var formattedSigningTime = signingTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Step 3: Construct the QualifyingProperties and hash it for the SignedProperties digest.
        var qualifyingProperties = new LhdnQualifyingProperties(
            Target: "signature",
            SignedProperties: new[]
            {
                new LhdnSignedProperties(
                    Id: "id-xades-signed-props",
                    SignedSignatureProperties: new[]
                    {
                        new LhdnSignedSignatureProperties(
                            SigningTime: formattedSigningTime,
                            SigningCertificate: new[]
                            {
                                new LhdnSigningCertificate(
                                    Cert: new[]
                                    {
                                        new LhdnCert(
                                            CertDigest: new[]
                                            {
                                                new LhdnCertDigest(
                                                    DigestMethod: new[] { new LhdnDigestMethod("", DigestMethodAlgo) },
                                                    DigestValue: certDigestBase64
                                                )
                                            },
                                            IssuerSerial: new[]
                                            {
                                                new LhdnIssuerSerial(issuerName, serialNumber)
                                            }
                                        )
                                    }
                                )
                            }
                        )
                    }
                )
            }
        );

        var propsHashBytes = HashDocument(qualifyingProperties);
        var propsBase64Digest = Convert.ToBase64String(propsHashBytes);

        // Step 4: Sign the canonicalized document bytes using RSA-SHA256 with PKCS#1 v1.5 padding.
        var rsa = certificate.GetRSAPrivateKey() ?? throw new InvalidOperationException("Certificate does not contain an RSA private key.");
        
#pragma warning disable CA1416 // LHDN specification strictly requires legacy PKCS#1 v1.5 padding. Do not upgrade to PSS.
        var signatureBytes = rsa.SignData(documentHashBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
#pragma warning restore CA1416
        
        var signatureValue = Convert.ToBase64String(signatureBytes);

        // Step 5: Assemble the final Signature and UBLExtensions blocks.
        var ublExtension = new LhdnUblExtensionWrapper(
            UBLExtension: new[]
            {
                new LhdnUblExtension(
                    ExtensionURI: ExtensionUri,
                    ExtensionContent: new[]
                    {
                        new LhdnExtensionContent(
                            UBLDocumentSignatures: new[]
                            {
                                new LhdnUblDocumentSignatures(
                                    SignatureInformation: new[]
                                    {
                                        new LhdnSignatureInformation(
                                            ID: SignatureId,
                                            ReferencedSignatureID: ReferencedSignatureId,
                                            Signature: new[]
                                            {
                                                new LhdnSignature(
                                                    Id: "signature",
                                                    Object: new[] { new LhdnSignatureObject(new[] { qualifyingProperties }) },
                                                    KeyInfo: new[]
                                                    {
                                                        new LhdnKeyInfo(
                                                            X509Data: new[]
                                                            {
                                                                new LhdnX509Data(
                                                                    X509Certificate: certContent,
                                                                    X509SubjectName: subjectName,
                                                                    X509IssuerSerial: new[] { new LhdnIssuerSerial(issuerName, serialNumber) }
                                                                )
                                                            }
                                                        )
                                                    },
                                                    SignatureValue: signatureValue,
                                                    SignedInfo: new[]
                                                    {
                                                        new LhdnSignedInfo(
                                                            SignatureMethod: new[] { new LhdnSignatureMethod("", SignatureMethodAlgo) },
                                                            Reference: new[]
                                                            {
                                                                new LhdnReference(
                                                                    Type: "http://uri.etsi.org/01903/v1.3.2#SignedProperties",
                                                                    URI: "#id-xades-signed-props",
                                                                    DigestMethod: new[] { new LhdnDigestMethod("", DigestMethodAlgo) },
                                                                    DigestValue: propsBase64Digest
                                                                ),
                                                                new LhdnReference(
                                                                    Type: "",
                                                                    URI: "",
                                                                    DigestMethod: new[] { new LhdnDigestMethod("", DigestMethodAlgo) },
                                                                    DigestValue: documentBase64Digest
                                                                )
                                                            }
                                                        )
                                                    }
                                                )
                                            }
                                        )
                                    }
                                )
                            }
                        )
                    }
                )
            }
        );

        var signatureReference = new LhdnSignatureReference(ReferencedSignatureId, ExtensionUri);

        // Step 6: Mutate the document to append the extensions at the end of the Invoice array element.
        var signedInvoice = document.Invoice[0] with
        {
            UBLExtensions = new object[] { ublExtension },
            Signature = new object[] { signatureReference }
        };

        var finalDocument = document with { Invoice = new[] { signedInvoice } };

        return new JsonSigningResult(
            SignedPayload: finalDocument,
            Base64Digest: documentBase64Digest,
            HexDigest: documentHexDigest,
            SignatureValue: signatureValue
        );
    }

    private static byte[] HashDocument<T>(T document)
    {
        using var memoryStream = new MemoryStream();
        JsonSerializer.Serialize(memoryStream, document, LhdnJsonOptions.Instance);
        memoryStream.Position = 0;
        return SHA256.HashData(memoryStream);
    }

    private static string ParseDistinguishedName(string dn)
    {
        var parts = dn.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
        Array.Reverse(parts);
        return string.Join(", ", parts);
    }

    private static string ParseSerialNumber(string hexString)
    {
        var bytes = Convert.FromHexString(hexString);
        Array.Reverse(bytes);
        return new System.Numerics.BigInteger(bytes, isUnsigned: true).ToString();
    }
}
